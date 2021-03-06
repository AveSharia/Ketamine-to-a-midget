﻿using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using HTMLUtils;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            //Blocking forward progress while I wait for the Http requests for player info
            //UTF8 encoding required for Chinese characters (but still won't work in console window)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            //dayaStuff.myData is the save follow for followed players. It is currently a binary file; a move to XML may make sense later to make it easier to hack/extend.
            string followedfileName = "dataStuff.myData";
            string cachedPageFileName = "postPageCache.myData";

            List<personObject> tlPeople = new List<personObject>();
            List<personObject> followedTLPeople = new List<personObject>();
            //Not actually saving/serializing these now; just for caching during a single use
            List<tlCachedPostPage> cachedPostPages = new List<tlCachedPostPage>();

            //This grabs all the players listed by continent on Liquipedia
            ScrapeGlobalPlayerLists(tlPeople).Wait();
            
            //Default ordering is by Liquipedia Name
            tlPeople = tlPeople.OrderBy(o => o.liquipediaName).ToList();

            Console.WriteLine("Done! " + tlPeople.Count.ToString() + " players found!");

            DeserializeFollowedPlayers(followedfileName, tlPeople, followedTLPeople); //Take players from "followedfilename" and put them into List tlPeople (or create fileName if it doesn't exist)
            DeserializeCachedPages(cachedPageFileName, cachedPostPages, tlPeople); //Take the posts from "cachedPagesFileName" and put them into List cachedPostPages (or create fillename if it doesn't exist)
            
            int quitThisGame = 0;

            while (quitThisGame == 0)
            {
                Console.WriteLine("Choose one of the following options: \n" +
                                        "A. Sort list by some property \n" +
                                        "B. Follow a user \n" +
                                        "C. Unfollow a user \n" +
                                        "D. Print the list, as sorted \n" +
                                        "E. Print the followed user list \n" +
                                        "F. Print player detail \n" +
                                        "G. Print player TL posts Summary \n" +
                                        "H. View a summary of Cached thread pages \n" +
                                        "I. Update all followed player threads \n" +
                                        "Q. Quit");

                Console.WriteLine();
                string inKey = Console.ReadLine().ToUpper();
                Console.WriteLine();
                switch (inKey)
                {
                    case "A":
                        Console.WriteLine("Choose a property to sort the list by: \n" +
                                        "A. Liquipedia Name \n" +
                                        "B. Real Name \n" +
                                        "C. Team Name \n" +
                                        "D. Country \n" +
                                        "E. Main Race \n" +
                                        "Any other key to quit.");
                        Console.WriteLine();
                        string inKey2 = Console.ReadLine().ToUpper();
                        Console.WriteLine();
                        switch (inKey2)
	                        {
                            case "A":
                                    tlPeople = tlPeople.OrderBy(o => o.liquipediaName).ToList();
                                    break;
                            case "B":
                                    tlPeople = tlPeople.OrderBy(o => o.irlName).ToList();
                                    break;
                            case "C":
                                    tlPeople = tlPeople.OrderBy(o => o.teamName).ToList();
                                    break;
                            case "D":
                                    tlPeople = tlPeople.OrderBy(o => o.country).ToList();
                                    break;
                            case "E":
                                    tlPeople = tlPeople.OrderBy(o => o.mainRace).ToList();
                                    break;
                            default:
                                    break;
	                        }
                        break;
                    case "B":
                        Console.WriteLine("Type the Name of the person to follow:");
                        Console.WriteLine();
                        string personToFollow = Console.ReadLine().ToUpper();
                        followAndSerialize(personToFollow, tlPeople, followedTLPeople, followedfileName).Wait();
                        break;
                    case "C":
                        Console.WriteLine("Type the Liquipedia Name of the person to unfollow:");
                        Console.WriteLine();
                        string personToUnfollow = Console.ReadLine().ToUpper();
                        unfollowAndStopSerializing(personToUnfollow, tlPeople, followedTLPeople, followedfileName);
                        break;
                    case "D":
                        foreach (personObject person in tlPeople)
                        {
                            person.displayPersonProperties();
                        }
                        break;
                    case "E":
                        var listOfFollowed = (from v in followedTLPeople
                                              where v.followed
                                              select v);
                        foreach (personObject person in listOfFollowed)
                        {
                            person.displayPersonProperties();
                        }
                        break;
                    case "F":
                        Console.WriteLine("Type the Liquipedia Name of the person whose details you want to see:");
                        Console.WriteLine();
                        string personForDetailView = Console.ReadLine().ToUpper();
                        extractPersonDetail(personForDetailView, tlPeople).Wait();
                        break;
                    case "G":
                        Console.WriteLine("Type the name of the person whose TL post details you want to see:");
                        Console.WriteLine();
                        string tlForumNameForPosts = Console.ReadLine().ToUpper();
                        Console.WriteLine();
                        
                        personObject personForPosts = personObjectFromString(tlForumNameForPosts, tlPeople);

                        if (personForPosts == null || personForPosts.tlForumURI == null)
                        {
                            extractPersonDetail(personForPosts, tlPeople).Wait();
                        }
                            
                        if (personForPosts.tlName != null)
                        {
                            List<tlPostObject> listOfReturnedPosts = new List<tlPostObject>();
                            HttpClient client2 = new HttpClient();

                            using (client2) { 
                                listOfReturnedPosts = grabUsersTlPosts(personForPosts, client2, cachedPostPages, 8, tlPeople, followedfileName, cachedPageFileName).Result;
                            }

                            int countPosts = 1;

                            countPosts = displayPostList(listOfReturnedPosts, countPosts);

                            wipeAndRecreatePageCache(cachedPageFileName, cachedPostPages);

                            Console.WriteLine("Do you want to: \n" +
                                                "     A. View a post in situ on a thread page? \n" +
                                                "     Q. Return to the Main Menu?");
                            string inkey3 = Console.ReadLine().ToUpper();
                            Console.WriteLine();
                            switch (inkey3)
	                        {
                                case "Q":
                                    break;
                                case "A":
                                    Console.WriteLine("Type the number of the comment you want to see in context.");
                                    Console.WriteLine();
                                    int inkey6 = Convert.ToInt32(Console.ReadLine().ToUpper());
                                    Console.WriteLine();

                                    int threadID_ex = listOfReturnedPosts.ElementAt(inkey6 - 1).uniqueThreadId;
                                    Console.WriteLine(threadID_ex.ToString());
                                    int postID_ex = listOfReturnedPosts.ElementAt(inkey6 - 1).commentNumber;
                                    Console.WriteLine(postID_ex.ToString());
                                    Uri postUri_ex = listOfReturnedPosts.ElementAt(inkey6 - 1).commentUri;
                                    Console.WriteLine(postUri_ex.ToString());
                                    string threadName_ex = listOfReturnedPosts.ElementAt(inkey6 - 1).threadTitle;
                                    Console.WriteLine(threadName_ex);
                                    string thread_Uri_Stub_ex = listOfReturnedPosts.ElementAt(inkey6 - 1).threadStubUri.ToString();
                                    Console.WriteLine(thread_Uri_Stub_ex);
                                    Console.WriteLine();
                                    int offSet_ex = 0;
                                    Console.WriteLine("Hit any key to continue...");
                                    var inkey7 = Console.ReadKey();
                                    
                                    while (inkey7.KeyChar.ToString() != "Q")
                                    {
                                        Console.Clear();
                                        
                                        //Grabs the single Post
                                        tlPostObject postFromUri_ex = getComment(thread_Uri_Stub_ex, (postID_ex + offSet_ex), cachedPostPages, tlPeople, followedfileName, cachedPageFileName).Result;
                                    
                                        var personFollowed = from i in tlPeople
                                                             where (i.tlName == postFromUri_ex.Author)
                                                             select i;
                                    
                                        string followStar = "";
                                        string postsTotal = "";

                                        if (personFollowed.FirstOrDefault() != null)
                                        {
                                            if (personFollowed.FirstOrDefault().followed == true)
                                            { 
                                                followStar = "***";
                                            }
                                            postsTotal = ", " + personFollowed.FirstOrDefault().tlTotalPosts.ToString() + " posts";
                                        }
                                    
                                        Console.WriteLine("-------------------------------------------------------------------------------");
                                        Console.WriteLine("Comment #" + postFromUri_ex.commentNumber + " by user " + postFromUri_ex.Author + followStar + " " + postsTotal + " on " + postFromUri_ex.postDateTime);
                                        Console.WriteLine(postFromUri_ex.postContent);
                                        Console.WriteLine("-------------------------------------------------------------------------------");
                                        Console.WriteLine();
                                        Console.WriteLine("Press > and < to navigate posts. Q to quit.");

                                        inkey7 = Console.ReadKey();
                                        if (inkey7.KeyChar.ToString() == "<")
                                        {
                                            offSet_ex--;
                                        }
                                        else if (inkey7.KeyChar.ToString() == ">")
                                        {
                                            offSet_ex++;
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("That person doesn't have a TL forum profile!");
                        }
                        break;
                    case "H":
                        foreach (tlCachedPostPage p in cachedPostPages)
                        {
                            Console.WriteLine(p.cachedPageRemoteUri);
                                Console.WriteLine("Page " + p.pageNumber.ToString() + ", " + p.posts.Count().ToString() + " comments found.");
                            
                            if (p.needsRefresh)
                            {
                                Console.WriteLine("This page is ripe for a refresh.");
                            }
                            else
                            {
                                Console.WriteLine("This is a recent cache and should be retrieved by default.");
                            }
                            Console.WriteLine();
                        }
                        Console.WriteLine("Found " + cachedPostPages.Count() + " cached thread pages total.");
                        break;
                    case "I":
                        int updatedPlayerCounter = updateAllFollowedPlayerPosts(followedfileName, cachedPageFileName, tlPeople, followedTLPeople, cachedPostPages).Result;

                        wipeAndRecreatePageCache(cachedPageFileName, cachedPostPages);

                        Console.WriteLine();
                        Console.WriteLine("Successfully updated " + updatedPlayerCounter + " people's posts out of " + followedTLPeople.Count().ToString() + " followed people total.");
                        Console.WriteLine();
                        Console.WriteLine("Press any key to view posts sorted by date/time");
                        Console.ReadKey();
                        List<tlPostObject> followedPosts = new List<tlPostObject>();
                        foreach (personObject h in followedTLPeople)
                        {
                            if ((h.tlPostList != null) && (h.tlPostList.Count() != 0))
                            {
                                followedPosts.AddRange(h.tlPostList);
                            }
                        }
                        var orderedPosts = from f in followedPosts
                                           orderby f.postDateTime
                                           select f;    //The orderby is going to be totally broken now... :/

                        foreach (tlPostObject e in orderedPosts)
                        {           
                        Console.WriteLine("-------------------------------------------------------------------------------");
                        Console.WriteLine("Comment #" + e.commentNumber + " by user " + e.Author + " on " + e.postDateTime);
                        Console.WriteLine();
                        Console.WriteLine(e.postContent);
                        Console.WriteLine("-------------------------------------------------------------------------------");
                        Console.WriteLine();
                        }

                        Console.WriteLine("Any key to quit.");
                        Console.ReadKey();
                        
                        break;
                    case "Q":
                        quitThisGame = 1;
                        break;
                    default:
                        break;
                }
                Console.WriteLine();
            }


        }

        private static async Task<int> updateAllFollowedPlayerPosts(string followedfileName, string cachedPageFileName, List<personObject> tlPeople, List<personObject> followedTLPeople, List<tlCachedPostPage> cachedPostPages)
        {
            int updatedPlayerCounter = 0;

            System.TimeSpan delayTime = new System.TimeSpan(0, 0, 3);
            System.DateTime continueWhen = System.DateTime.Now; //This will start the first search right away

            foreach (personObject followedPlayer in followedTLPeople)
            {
                if (followedPlayer.tlName == null)
                {
                    extractPersonDetail(followedPlayer, tlPeople).Wait();
                }

                if (followedPlayer.tlName != null)
                {
                    HttpClient groupclient = new HttpClient();

                    using (groupclient) { 
                        Console.WriteLine("Grabbing " + followedPlayer.tlName + "'s posts.");

                        while (System.DateTime.Now < continueWhen)
                        {
                            Console.WriteLine("Waiting half a second to avoid TL's rate limit.");
                            await Task.Delay(500);
                        }

                        continueWhen = System.DateTime.Now.Add(delayTime);
                        List<tlPostObject> listOfEveryonesPosts = await grabUsersTlPosts(followedPlayer, groupclient, cachedPostPages, 3, tlPeople, followedfileName, cachedPageFileName);
                        updatedPlayerCounter++;
                        }
                }
            }
            return updatedPlayerCounter;
        }

        private static void wipeAndRecreatePageCache(string cachedPageFileName, List<tlCachedPostPage> cachedPostPages)
        {
            Console.WriteLine("Serializing all cached post pages.");
            File.Delete(cachedPageFileName);
            foreach (tlCachedPostPage w in cachedPostPages)
            {
                StartSerializingCachedPage(cachedPageFileName, w);
            }
            Console.WriteLine("Done serializing.");
            Console.WriteLine();
        }

        private static int displayPostList(List<tlPostObject> listOfReturnedPosts, int countPosts)
        {
            try
            {
                foreach (tlPostObject n in listOfReturnedPosts)
                {
                    Console.WriteLine("[" + n.Author + "'s Comment Number " + countPosts + "]");
                    Console.WriteLine(n.threadTitle);
                    Console.WriteLine("Comment by " + n.Author);
                    Console.WriteLine("Comment #" + n.commentNumber + ":");
                    //Console.WriteLine(n.postContent);
                    countPosts++;
                    Console.WriteLine();
                }
            }
            catch
            {
                Console.WriteLine("Collection changed. Starting over.");
                countPosts = 1;
                countPosts = displayPostList(listOfReturnedPosts, countPosts);
            }
            return countPosts;
        }

        /// <summary>
        /// Given the Uri of a single page of a thread, returns a string giving the stub URL of the first page of that thread.
        /// </summary>
        /// <param name="thread_Uri">A Uri object pointing to a single page of the thread</param>
        /// <returns></returns>
        private static string ThreadStubStringFromThreadPageUri(Uri thread_Uri)
        {
            string uriToString = thread_Uri.ToString();
            int qLoc = uriToString.IndexOf("?page=");
            if (qLoc == -1)
            {
                return uriToString;
            }
            else
            { 
                return uriToString.Substring(0, qLoc - 1);
            }
        }

        static personObject personObjectFromString(string personString, List<personObject> tlPeople)
        {
            var person = (from u in tlPeople
                          where u.liquipediaName != null &&
                          u.liquipediaName.ToUpper() == personString.ToUpper()
                          select u);
            if (person.FirstOrDefault() == null)
            {
                Console.WriteLine("Person not found in Liquipedia. Checking for a saved TL.net forum profile...");
                var person2 = (from v in tlPeople
                               where v.tlName != null && 
                               v.tlName.ToUpper() == personString.ToUpper()
                               select v);
                if (person2.FirstOrDefault() == null)
                {
                    Console.WriteLine("No saved TL.net profile. Searching TL.net...");
                    HttpClient tlForumClient = new HttpClient();

                    string profileString;
                    
                    using(tlForumClient)
                    { 
                        profileString = HTMLUtilities.getHTMLStringFromUriAsync(tlForumClient, new Uri("http://www.teamliquid.net/forum/profile.php?user=" + personString)).Result;
                    }

                    if (profileString == null)
                    { 
                        return null;
                    }
                    else
                    {
                        //Create a new personObject based on the TL.net profile page alone
                        personObject tlPersonObj = new personObject();
                        //Looking to scrape <title>Public Profile for TheDwf</title>
                        
                        tlPersonObj.tlName = getTextBetween(profileString, "<title>Public Profile for ", "</title>");
                        
                        tlPersonObj.tlForumURI = new Uri("http://www.teamliquid.net/forum/profile.php?user=" + tlPersonObj.tlName);
                        string numPostsTags = HTMLUtilities.StringFromTag(profileString, "<a href='search.php?q=&amp;t=c&amp;f=-1&u=", "</a>");
                        string numPosts = HTMLUtilities.InnerText(numPostsTags, 0, numPostsTags.Length);
                        if (numPosts != null)
                        { 
                            tlPersonObj.tlTotalPosts = Convert.ToInt32(numPosts);
                        }
                        tlPeople.Add(tlPersonObj);
                        return tlPersonObj;
                    }
                }
                else
                {
                    return person2.FirstOrDefault();
                }
            }
            else
            {
                return person.FirstOrDefault();
            }
        }

        private static string getTextBetween(string profileString, string openTag, string closeTag)
        {
            if ((profileString != null) && (profileString.Length > 2))
            { 
                string tlNameTag = HTMLUtilities.StringFromTag(profileString, openTag, closeTag);
                if (tlNameTag != null)
                {
                    string tlScrapedName = tlNameTag.Substring(openTag.Length, tlNameTag.Length - openTag.Length - closeTag.Length);
                    return tlScrapedName;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        static async Task<personObject> extractPersonDetail(string personForDetailView, List<personObject> tlPeople)
        {
            personObject person = personObjectFromString(personForDetailView, tlPeople);
            return await extractPersonDetail(person, tlPeople);
        }

        static async Task<personObject> extractPersonDetail(personObject person, List<personObject> tlPeople)
        {
            if (person == null)
            {
                return await Task.Run(() => person);
            }
            else
            {
                if (person.liquipediaURI != null)
                {
                    //1. Load async the players teamliquid.net profile URL
                    using (var client = new HttpClient())
                    {
                        Uri playerDetailUri = new Uri(person.liquipediaURI.ToString());

                        var response = await client.GetAsync(playerDetailUri);

                        if (response.IsSuccessStatusCode)
                        {
                            UTF8Encoding utf8 = new UTF8Encoding();
                            //string responseString = utf8.GetString(response);
                            string responseString = await response.Content.ReadAsStringAsync();

                            string infoBox_tags = HTMLUtilities.StringFromTag(responseString, "<div class=\"infobox-center infobox-icons\">", "</div>");

                            //2. Scrape that page for the rest of the detail properties
                            //3. Fill those (switch like the main list scraper)

                            person.tlForumURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "TeamLiquid.net Profile");
                            if (person.tlForumURI != null) person.tlName = tlNameFromURI(person.tlForumURI);
                            person.twitterURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Twitter");
                            if (person.twitterURI != null) person.twitterName = twitterNameFromURI(person.twitterURI);
                            person.fbURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Facebook");
                            if (person.fbURI != null) person.fbName = fbNameFromURI(person.fbURI);
                            person.twitchURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Twitch Stream");
                            if (person.twitchURI != null) person.twitchName = twitchIDfromURI(person.twitchURI);
                            person.redditProfileURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Reddit Profile");
                            if (person.redditProfileURI != null) person.redditUsername = redditNameFromURI(person.redditProfileURI);

                            //There are some other tags, e.g., battle.net urls, that show up later under "external links"
                            //Since I really want to move on to pulling the posts from TL, I'm putting off grabbing that
                            //stuff until later.

                            //If a Tl.net profile URI exists, scrape that page for information (how often posts, total posts (I think I should serialize this), etc.)
                            if (person.tlForumURI != null)
                            {
                                string tlProfilePageString = await HTMLUtilities.getHTMLStringFromUriAsync(client, person.tlForumURI);
                                if (tlProfilePageString != null)
                                { 
                                    string numPostsTags = HTMLUtilities.StringFromTag(tlProfilePageString, "<a href='search.php?q=&amp;t=c&amp;f=-1&u=", "</a>");
                                    string numPosts = HTMLUtilities.InnerText(numPostsTags, 0, numPostsTags.Length);
                                    if (numPosts != null)
                                    { 
                                        person.tlTotalPosts = Convert.ToInt32(numPosts);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("That tlForumURI link doesn't work. Is Liquipedia wrong about " + person.tlName + "'s TL username?");
                                }
                            }
                        }
                    }
                }
            //4. Display the details (will ultimately return)
            if (person.tlName != null) Console.WriteLine(person.tlName + " on teamliquid: " + person.tlForumURI);
            if (person.twitterName != null) Console.WriteLine(person.twitterName + " on Twitter: " + person.twitterURI);
            if (person.fbName != null) Console.WriteLine(person.fbName + " on Facebook: " + person.fbURI);
            if (person.twitchName != null) Console.WriteLine(person.twitchName + " on Twitch.tv: " + person.twitchURI); //updates the one scraped from the countries list, for uniformity
            if (person.redditUsername != null) Console.WriteLine(person.redditUsername + " on Reddit: " + person.redditProfileURI);
            if (person.tlTotalPosts != 0) Console.WriteLine("Total posts on TeamLiquid.net: " + person.tlTotalPosts);
            }
            return await Task.Run(() => person);
        }

        private static async Task<List<tlPostObject>> grabUsersTlPosts(personObject person, HttpClient client, List<tlCachedPostPage> cachedPostPages, int postsToGrab, List<personObject> tlPeople, string fileName, string cachedPageFileName)
        {
            //It's actually better to grab 4+ of these posts... otherwise the searches for individual posters happen too fast and TL rate limits you.
            List<tlPostObject> returnedPosts = new List<tlPostObject> { };
            Uri postsPage = tlPostUriFromTlUsername(person.tlName);
            string tlPostsResultPage = await HTMLUtilities.getHTMLStringFromUriAsync(client, postsPage);
            if (tlPostsResultPage.IndexOf("You are performing searches too quickly") != -1)
            {
                Console.WriteLine("You searched too fast. Slow down!");
                System.Threading.Thread.Sleep(5000);
                
                HttpClient waitClient = new HttpClient();
                using (waitClient) { 
                    
                    tlPostsResultPage = await HTMLUtilities.getHTMLStringFromUriAsync(waitClient, postsPage);
                    
                    if (tlPostsResultPage.IndexOf("You are performing searches too quickly") != -1)
                    {
                        Console.WriteLine("You are STILL performing searches too quickly!!! Aborting.");
                        Console.ReadKey();
                    }
                }
            }

            string postsBlock = HTMLUtilities.StringFromTag(tlPostsResultPage, "<tr><td class='srch_res1'>", "</td></tr></TABLE>");
            int readPosition = 0;
            int threadCount = 0;
            int postCount = 0;
            string srch_res_toggle = "1";

            List<Task> taskFactoryTasks = new List<Task>();
            List<Task<tlPostObject>> grabbedPostList = new List<Task<tlPostObject>>();
            //List<tlPostObject> grabbedPostList = new List<tlPostObject>();

            while (readPosition != -1 && readPosition < postsBlock.Length && postCount < postsToGrab)
            {
                //Have to read through by TDs, but add list items by individual links
                //So, keep track of post general information by TD, then add it all at each link
                //(So there will be a while loop in this while loop)

                int threadBlock_start = postsBlock.IndexOf("<tr><td class='srch_res", readPosition);
                int threadBlock_end = postsBlock.IndexOf("</td></tr>", threadBlock_start) + "</td></tr>".Length;
                readPosition = threadBlock_start;
                string threadBlock = postsBlock.Substring(threadBlock_start, threadBlock_end - threadBlock_start);
                string post_forum_block = HTMLUtilities.StringFromTag(threadBlock, "<td class='srch_res" + srch_res_toggle + "'><font size='-2' color='#808080'>", "</font>");
                string post_forum = HTMLUtilities.InnerText(post_forum_block, 0, post_forum_block.Length);
                if (post_forum != null)
                {
                    post_forum = post_forum.TrimEnd(":".ToCharArray());
                }
                string thread_title_block = HTMLUtilities.StringFromTag(threadBlock, "<a class='sl' name='srl' href=", "</a>");
                string thread_title = HTMLUtilities.InnerText(thread_title_block, 0, thread_title_block.Length);
                
                string thread_Uri_stub = "http://www.teamliquid.net" + HTMLUtilities.grabHREF(thread_title_block);
                int UniqueThreadID = threadIdFromThreadUriString(thread_Uri_stub);

                //Process thread posts here
                int post_list_block_start = threadBlock.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=");
                int post_list_block_end = threadBlock.IndexOf("</td>", post_list_block_start);
                int post_list_block_length = post_list_block_end - post_list_block_start;
                string post_list_block = threadBlock.Substring(post_list_block_start, post_list_block_length);
                int subThread_position = 0;
                int thisPost_pageNum = 0;
                int lastPost_pageNum = 0;

                while (subThread_position != -1 && postCount < postsToGrab)
                {
                    //This is NOT DUPLICATING the block immediately before; it handles situations where there are more than
                    //one comment per thread.
                    int postLink_start = post_list_block.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=", subThread_position);
                    int postLink_end = post_list_block.IndexOf("</a>", postLink_start) + "</a>".Length;
                    int postLink_length = postLink_end - postLink_start;
                    string postLink_tags = post_list_block.Substring(postLink_start, postLink_length);
                    Uri postLink = new Uri("http://www.teamliquid.net/forum/" + HTMLUtilities.grabHREF(postLink_tags));
                    int postNumber = Convert.ToInt32(HTMLUtilities.InnerText(postLink_tags, 0, postLink_tags.Length));
                    if (postNumber == 0)
                    {
                        postNumber = 1; //Probably the first post of a thread, by the TL staff.
                    }
                    subThread_position = post_list_block.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=", postLink_end);

                    //tlPostObject requestedPost = await getComment(thread_Uri_stub, postNumber, cachedPostPages, tlPeople, fileName);
                    //Separate instanced where a comment is the first to be scraped on it's page?

                    thisPost_pageNum = pageNumFromPostNum(postNumber);

                    if (thisPost_pageNum != lastPost_pageNum)
                    {
                        taskFactoryTasks.Add(Task.Factory.StartNew(() => grabbedPostList.Add(getComment(thread_Uri_stub, postNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName))));
                        //grabbedPostList.Add(getComment(thread_Uri_stub, postNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName).Result);
                    }
                    else
                    {
                        //This delayed task is running up against the one it is supposed to be waiting on...
                        //returnedPosts.Add(getComment(thread_Uri_stub, postNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName).Result);
                        Task cachedCommentTask = taskFactoryTasks.Last().ContinueWith((antecedent) =>
                                                    {
                                                        returnedPosts.Add(getComment(thread_Uri_stub, postNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName).Result);
                                                    });
                    }

                    lastPost_pageNum = pageNumFromPostNum(postNumber);

                    postCount++;
                }


                readPosition += threadBlock.Length;

                if (srch_res_toggle == "1")
                    {
                        srch_res_toggle = "2";
                    }
                    else
                    {
                        srch_res_toggle = "1";
                    }
                threadCount++;
            }

            while ((taskFactoryTasks.Count > 0) || (grabbedPostList.Count() > 0))
            {
                Task firstFactoryTask = await Task.WhenAny(taskFactoryTasks);
                taskFactoryTasks.Remove(firstFactoryTask);

                Task<tlPostObject> firstProcessedPost = await Task.WhenAny(grabbedPostList);
                //tlPostObject firstProcessedPost = grabbedPostList.First<tlPostObject>();
                grabbedPostList.Remove(firstProcessedPost);
                tlPostObject thisProcessedPost = await firstProcessedPost;
                returnedPosts.Add(thisProcessedPost);
            }

            //while (taskFactoryTasks.Count > 0)
            //{
            //    Task firstFactoryTask = await Task.WhenAny(taskFactoryTasks);
            //    taskFactoryTasks.Remove(firstFactoryTask);
            //}

            //while (grabbedPostList.Count() > 0)
            //{
            //    Task<tlPostObject> firstProcessedPost = await Task.WhenAny(grabbedPostList);
            //    grabbedPostList.Remove(firstProcessedPost);
            //    tlPostObject thisProcessedPost = await firstProcessedPost;
            //    returnedPosts.Add(thisProcessedPost);
            //}

            //Need to make sure all threads are done before returning... this is continuing to modify Lists after returning!
            Task.WaitAll();
            return await Task.Run(() => returnedPosts);
        }

        /// <summary>
        /// Returns a tlPostObject from a TL thread Uri stub and a post number. If the post is cached, it uses the cache; if not, is scrapes and caches the page on which the comment appears. 
        /// </summary>
        /// <param name="thread_Uri_stub">Everything before "?page=" in a TL.net thread Uri (parameter is a string).</param>
        /// <param name="postNumber">The individual comment number as it appears in the thread.</param>
        /// <param name="cachedPostPages">The list of cached pages.</param>
        /// <param name="tlPeople">The list of people.</param>
        /// <returns></returns>
        private static async Task<tlPostObject> getComment(string thread_Uri_stub, int postNumber, List<tlCachedPostPage> cachedPostPages, List<personObject> tlPeople, string fileName, string cachedPageFileName)
        {
            tlPostObject requestedPost = new tlPostObject();
            //I think I should use a separate client for each comment. That way, in the future, I can grab the pages concurrently
            //in separate threads. For now, I'm waiting in between.

            //Check to see if the post page has already been cached; you should have added a property to the tlPostObject if it has been
            tlCachedPostPage cachedPage = getCachedPage(cachedPostPages, thread_Uri_stub, postNumber);

            //If the page is ripe for a refresh, do it (shouldn't really return anything)

            //This is now a bit more complicated... need to scrape the page, add postObjects to the cachedPageObject's list of posts
            //Since detecting edits/deletions would require comparing individual text, it probably just makes more sense to overwrite
            //any existing postObjects for that page. As for the player... you shouldn't need to check. Their list references the same objects
            //as the cachedPageObject's list, so as long as you're updating the object information (and not replacing with new objects,)
            //it should just work.

            int threadPageNumber = pageNumFromPostNum(postNumber);
            Uri thread_page_Uri = new Uri(thread_Uri_stub + "?page=" + threadPageNumber.ToString());

            if (cachedPage.needsRefresh)
            {
                HttpClient commentClient = new HttpClient();
                using (commentClient)
                { 
                    Console.WriteLine("Reading page from the web...");
                    requestedPost = await grabPostAndCachePage(cachedPage, commentClient, thread_page_Uri, postNumber, tlPeople, cachedPostPages, fileName, cachedPageFileName);
                }

                if (requestedPost == null)
                {
                    return null;
                } else
                {
                    requestedPost.threadStubUri = new Uri(thread_Uri_stub);
                    cachedPage.needsRefresh = false;
                }
                //...just pass the cache page (which has to exist at this point) and have it update values right in the method
                //This is fine because literally every time you pull the HttpRequest, you should update the object cache

                //Logic for whether to cache neighbors should go here
                if ((requestedPost.commentNumber < (20 * cachedPage.pageNumber - 18)) && (cachedPage.prevThreadPage != null))
                {
                    //check for previous cached page, and if none, scrape cachedPage.prevThreadPage for post 20*(cachedPage.pageNumer - 1)
                    var cachedPostMatch = (from o in cachedPostPages
                                           where o.cachedPageUniqueThreadID == cachedPage.cachedPageUniqueThreadID
                                           && o.pageNumber == (cachedPage.pageNumber - 1)
                                           select o).FirstOrDefault();
                    if (cachedPostMatch == null)
                    {
                        int prevPostNumber = (20 * (cachedPage.pageNumber - 1));
                        Uri postLinkUri = new Uri(cachedPage.prevThreadPage.ToString());
                        Console.WriteLine("     Retrieving the next page...");
                        Console.Write("     ");
                        await getComment(thread_Uri_stub, prevPostNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName);
                    }

                }
                else if ((requestedPost.commentNumber > (20 * cachedPage.pageNumber - 2)) && (cachedPage.nextThreadPage != null))
                {
                    //check for next cached page, and if none, scrape cachedPage.nextThreadPage for post 20*(cachedPage.pageNumer) + 1
                    var cachedPostMatch = (from o in cachedPostPages
                                           where o.cachedPageUniqueThreadID == cachedPage.cachedPageUniqueThreadID
                                           && o.pageNumber == (cachedPage.pageNumber + 1)
                                           select o).FirstOrDefault();
                    if (cachedPostMatch == null)
                    {
                        int nextPostNumber = (20 * (cachedPage.pageNumber) + 1);
                        Uri postLinkUri = new Uri(cachedPage.nextThreadPage.ToString());
                        Console.WriteLine("     Retrieving the next page from the web...");
                        Console.Write("     ");
                        await getComment(thread_Uri_stub, nextPostNumber, cachedPostPages, tlPeople, fileName, cachedPageFileName);
                    }
                }

            }
            else
            {
                Console.WriteLine("Reading page from the cache...");

                //If it doesn't need a refresh... grab the existing post object
                requestedPost = cachedPage.posts.Where(s => s.commentNumber == postNumber).FirstOrDefault();
            }
            return requestedPost;
        }

        /// <summary>
        /// Returns a cached page for a post if a cached page exists, or creates one if it doesn't.
        /// </summary>
        /// <param name="cachedPostPages">The list of cached pages.</param>
        /// <param name="thread_Uri_stub">The Uri stub of the thread URL; that is, everything before "?page="</param>
        /// <param name="postNumber">The post number you are retrieving the cached page for</param>
        /// <returns></returns>
        private static tlCachedPostPage getCachedPage(List<tlCachedPostPage> cachedPostPages, string thread_Uri_stub, int postNumber) //You should overload this to take a post object
        {
            int UniqueThreadID = threadIdFromThreadUriString(thread_Uri_stub);
            
            //This LINQ statement grabs ALL thread pages that have the same Unique Thread ID 
            List<tlCachedPostPage> CachedPagesThisThread = new List<tlCachedPostPage>();

            CachedPagesThisThread = queryCacheSafeFromChanges(CachedPagesThisThread, UniqueThreadID, cachedPostPages);

            tlCachedPostPage match = new tlCachedPostPage();

            if (CachedPagesThisThread.FirstOrDefault() != null)
            {
                try { 
                //I think the bug here is that somehow the earlier Task calls are adding empty cache pages?
                match = CachedPagesThisThread.Where(q => q.posts != null && q.posts.Any(li => li.commentNumber == postNumber)).FirstOrDefault();
                }
                catch
                {
                    Console.WriteLine("Bugger bugger bug.");
                }
            }
            else
            {
                match = null;
            }

            //Did you find a matching post result?
            if (match == null)
            {
                //  There is no cache page yet. Create a cachePage object for it,
                tlCachedPostPage cachedPageObject = new tlCachedPostPage();
                cachedPageObject.cachedPageRemoteUri = new Uri(thread_Uri_stub);
                cachedPageObject.needsRefresh = true;
                cachedPageObject.cachedPageUniqueThreadID = UniqueThreadID;
                cachedPageObject.prevThreadPage = null;
                cachedPageObject.nextThreadPage = null;

                //  ...add it to the list of cached pages, return it, and...
                cachedPostPages.Add(cachedPageObject);
                return cachedPageObject;
                //  ...add the posts on the page to it (do this later, so that you don't have to call it twice) 
            }
            else
            {
                //  There is a cache page for it! Check to see if it is ripe for a refresh.
                tlCachedPostPage cachedPageObject = (tlCachedPostPage)match;
                return cachedPageObject;
            }    
        }

        private static List<tlCachedPostPage> queryCacheSafeFromChanges(List<tlCachedPostPage> CachedPagesThisThread, int UniqueThreadID, List<tlCachedPostPage> cachedPostPages)
        {
            try
            {
                CachedPagesThisThread = (from u in cachedPostPages
                                         where (u != null) && (u.cachedPageUniqueThreadID == UniqueThreadID)
                                         select u).ToList();
            }
            catch
            {
                Console.WriteLine("Collection was modified; re-starting search query.");
                return queryCacheSafeFromChanges(CachedPagesThisThread, UniqueThreadID, cachedPostPages);
            }
            return CachedPagesThisThread;
        }

        private static tlCachedPostPage getCachedPage(List<tlCachedPostPage> cachedPostPages, tlPostObject postObject) //Overload for post object
        {
            string Uri_thread_stub = ThreadStubStringFromThreadPageUri(postObject.commentUri);
            return getCachedPage(cachedPostPages, Uri_thread_stub, postObject.commentNumber);
        }

        static Uri tlPostUriFromTlUsername(string tlUsername)
        {
            return new Uri("http://www.teamliquid.net/forum/search.php?q=&t=c&f=-1&u=" + tlUsername + "&gb=date&d=");
        }

        /// <summary>
        /// Returns the page number of a comment from that comment's comment number in the thread, based on 20 comments per page.
        /// </summary>
        /// <param name="PostNum">The number identifier of the comment in its thread.</param>
        /// <returns></returns>
        static int pageNumFromPostNum(int PostNum)
        {
            //From Excel, = 1 + (A34-1)/20 - MOD((A34-1), 20)/20
            return (1 + (PostNum - 1)/20 - ((PostNum - 1) % 20)/20);
        }

        private static async Task<tlPostObject> grabPostAndCachePage(tlCachedPostPage cachedPage, HttpClient client, Uri postLink, int postNumber, List<personObject> tlPeople, List<tlCachedPostPage> cachedPostPages, string fileName, string cachedPageFileName)
        {
            tlPostObject returnPost = new tlPostObject();

            string threadPage = await HTMLUtilities.getHTMLStringFromUriAsync(client, postLink);
            string returnString = null;

            if (threadPage == null)
            {
                return null;
            }

            //Scrape the unique thread ID
            string threadLinkBlock = HTMLUtilities.StringFromTag(threadPage, "<link rel=\"canonical\"", "</head>");
            string postLinkString = HTMLUtilities.grabHREF(threadLinkBlock);
            int thread_id = threadIdFromThreadUriString(postLinkString);
            cachedPage.cachedPageUniqueThreadID = thread_id;

            //Scrape the thread title - this includes the page number, which sucks, but removing it isn't a priority
            string thread_title = HTMLUtilities.InnerText(HTMLUtilities.StringFromTag(threadPage, "<title>", "</title>"));
            int threadTitlePageNumIndex = thread_title.IndexOf(" - Page");
            if (threadTitlePageNumIndex != -1)
            {
                thread_title = thread_title.Substring(0, threadTitlePageNumIndex);
            }
            
            //Scrape the post forum
            int subforumStart = threadPage.LastIndexOf("<span itemprop=\"title\">") + "<span itemprop=\"title\">".Length;
            string subForumEndString = "</span></a></span>";
            int subforumLength = threadPage.IndexOf(subForumEndString, subforumStart) - subforumStart;
            string postSubforum = threadPage.Substring(subforumStart, subforumLength);

            //Scrape the number of pages in this thread, and whether there is a previous or next one (useful later when
            //scraping adjacent pages
            int currentPage = 0;
            string pagesBlock = "";
            bool isThereANextPage = new bool();
            
            int pagesBlockStart = threadPage.IndexOf("<div class=\"pagination\">", subforumStart + subforumLength);
            int pagesBlockEnd = threadPage.IndexOf("</div>", pagesBlockStart) + "</div>".Length;
            int pagesBlockLength = pagesBlockEnd - pagesBlockStart;
            pagesBlock = threadPage.Substring(pagesBlockStart, pagesBlockLength);
            int currPageStart = pagesBlock.IndexOf("<b>") + "<b>".Length;

            if (currPageStart == -1 + "<b>".Length)
            {
                //This is the only page in the thread.
                currentPage = 1;
                isThereANextPage = false;
            }
            else
            {
                int currPageEnd = pagesBlock.IndexOf("</b>", currPageStart);
                int currPageLength = currPageEnd - currPageStart;
                currentPage = Convert.ToInt32(pagesBlock.Substring(currPageStart, currPageLength));
            }

            if (pagesBlock.IndexOf("?page=" + (currentPage + 1).ToString()) == -1)
            {
                //This is the last page; there isn't one after it.
                isThereANextPage = false;
            }
            else
            {
                //There is another page!
                isThereANextPage = true;
            }

            //Scrape the individual posts!
            int readPosition = 0;
            string startBlock = "<table id=\"forumtable\"";
            string endBlock = "<div class=\"fpost-actionable\">";
            List<Task> grabTlPostListTasks = new List<Task>();

            while (readPosition != -1)
            {
                string commentBlock = HTMLUtilities.StringFromTag(threadPage, startBlock, endBlock, readPosition);
                if (commentBlock == null)
                {
                    Console.WriteLine("This page doesn't have any comments!");
                }
                //Scrape the post blocks here
                
                //Scrape the post link
                string singlePostLink = HTMLUtilities.StringFromTag(commentBlock, "<a href=\"/forum/viewpost.php?post_id=", " class=\"submessage\"");
                
                if (singlePostLink != null)
                {
                    singlePostLink = "http://www.teamliquid.net" + HTMLUtilities.grabHREF(singlePostLink);
                }else
                { 
                    //This is probably a TL self-post (e.g. QXC). Need to grab more than current commentBlock
                    singlePostLink = postLinkString;
                    string startTLlink = "<a href=\"/forum";
                    string endTLlink = "?page";
                    int singlePostTag_start = commentBlock.IndexOf(startTLlink);
                    if (singlePostTag_start != -1)
                    {
                        int singlePostTag_end = commentBlock.IndexOf(endTLlink, singlePostTag_start);
                    }
                    else
                    {
                        Console.WriteLine("Error out");
                    }
                }

                //Scrape the post Number [Not finding these strings in the TL-posts!]
                string singlePostString = HTMLUtilities.StringFromTag(commentBlock, "<div class=\"submessage\">#", "</div>");
                
                if (singlePostString != null)
                {
                    singlePostString = HTMLUtilities.InnerText(singlePostString);
                    singlePostString = singlePostString.Substring(1, singlePostString.Length - 1);
                }
                else
                {
                    singlePostString = "1";
                }
                int singlePostNumber = Convert.ToInt32(singlePostString);

                //Scrape the post Author
                string authorBlock = HTMLUtilities.StringFromTag(commentBlock, "<div class=\"fpost-username\">", "</div>");
                string authorName = null;

                if (authorBlock != null)
                {
                    string authorBegin = "<span>";
                    string authorEnd = "</span>";
                    int authorName_start = authorBlock.IndexOf(authorBegin) + authorBegin.Length;
                    int authorName_length = authorBlock.IndexOf(authorEnd, authorName_start) - authorName_start;
                    authorName = authorBlock.Substring(authorName_start, authorName_length);
                    //Console.WriteLine("Found a comment by " + authorName + "."); //For debugging scrape issues
                }
                else
                {
                    authorName = null;
                }

                //Scrape the total posts for the author, and, if the author is followed, and the number is higher than the recorded number, grab any new posts
                //<span class='forummsginfo'>&nbsp;<div class='usericon T10'></div>&nbsp;Pokebunny &nbsp; United States. December 16 2014 14:46. Posts 10276</span>
                string postTotalBlock = HTMLUtilities.StringFromTag(commentBlock, "</span><span class=\"tt-userinfo\">", "<article class=\"forumPost\">");
                string postTotalString = null;
                int postsTotal = 0;
                if (postTotalBlock != null)
                {
                    postTotalString = getTextBetween(postTotalBlock, "userinfo\">", " Posts");
                    if (postTotalString != null)
                    {
                        postsTotal = Convert.ToInt32(postTotalString);
                    }
                    else
                    {
                        //So far, this only occurs for the TL E-Sports self-posts. They just break everything.
                        postsTotal = 0;
                    }
                }
                else
                {
                    Console.WriteLine("This post doesn't have a post total block!");
                }

                //DateTime postDateTime = DateTime.MinValue;
                string postDateTime = "A long, long time ago";

                if (authorName != null)
                {
                    var foundAuthorTest = from h in tlPeople
                                      where h.tlName == authorName
                                      select h;
                    personObject foundAuthor = foundAuthorTest.FirstOrDefault();
                    if ((foundAuthor != null) && (foundAuthor.followed == true))
                    {
                        int postDifference = postsTotal - foundAuthor.tlTotalPosts;
                        if (postDifference > 0)
                        {
                            foundAuthor.tlTotalPosts = postsTotal;
                            //Need to re-serialize; not sure if this will totally break (double-follow) or not
                            //Also, cap this at something for now
                            StopSerializing(fileName, foundAuthor);
                            StartSerializingPerson(fileName, foundAuthor); //The single dumbest two lines of code I may ever write.
                            if (postDifference > 8)
                            {
                                postDifference = 8;
                            }
                            HttpClient updatePostsClient = new HttpClient();
                            //Could hypothetically run more than once because of the while block, so I will add to a List and await them at the end
                            //This way execution doesn't halt here; and grabPostAndCachePage runs asynchronously, so it won't hold up other grabs either
                            //Not sure how to test this since it's such a rare case... hopefully TheDwf posts sometime while I'm running it!
                            grabTlPostListTasks.Add(grabUsersTlPosts(foundAuthor, updatePostsClient, cachedPostPages, postDifference, tlPeople, fileName, cachedPageFileName));
                        }
                    }
                    
                    //Teamliquid has done away with posting dates/times, and now just has "X hours/weeks/days ago"
                    //string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
                    //int latestMonthIndex = -1;
                    //string latestMonth = null;
                    //foreach (string testmonth in months)
                    //{
                    //    int indexOfMonth = postTotalBlock.LastIndexOf(testmonth);
                    //    if (indexOfMonth > latestMonthIndex)
                    //    {
                    //        latestMonthIndex = indexOfMonth;
                    //        latestMonth = testmonth;
                    //    }
                    //}
                    
                    //if ((latestMonthIndex != -1) && (latestMonth != null))
                    //{
                        string startDate = "title=\"Link to this post\">";
                        string endDate = " ago</a>";
                        int startDateIndex = postTotalBlock.IndexOf(startDate);
                        int endDateIndex = postTotalBlock.IndexOf(endDate);
                        if ((endDateIndex != -1) && startDateIndex != -1) //&& (endDateIndex > latestMonthIndex))
                        {
                            int dateTimeLength = endDateIndex - startDateIndex - startDate.Length;
                            string postDateTimeString = postTotalBlock.Substring(startDateIndex + startDate.Length, dateTimeLength + 4);
                            //postDateTime = Convert.ToDateTime(postDateTimeString);
                            postDateTime = postDateTimeString;
                        }
                        else
                        {
                            //postDateTime = DateTime.MinValue;
                            postDateTime = "A long, long time ago";
                        }
                    //}
                }
                else
                {
                    authorName = "TeamLiquid ESPORTS";
                    string dateLine = HTMLUtilities.InnerText(HTMLUtilities.StringFromTag(commentBlock, "<div class=\"newspost_date\">", "</div>"));
                    //int dateDelimiterIndex = dateLine.IndexOf("|");
                    string tlesDateTimeString = dateLine.Replace("th,", "").Replace("rd,", "").Replace("st,","").Replace("nd,", "");
                    //postDateTime = Convert.ToDateTime(tlesDateTimeString);
                    postDateTime = tlesDateTimeString;
                }

                //Scrape the comment text
                string commentTags = HTMLUtilities.StringFromTag(commentBlock, "<article class=\"forumPost\">", "</article>"); //Same potential problem as above
                string commentHTML = HTMLUtilities.InnerText(commentTags, 0, commentTags.Length);
                
                //Impement updating the postObjects. Pseudocode (for outline purposes):
                //
                //              if cachedPage.postList contains the post
                //                  update the post info
                //                  you're done
                //              else
                //                  cachedPage.postList.Add(the post)
                //                  if the person is a personObject
                //                      person.postList.Add(the post)
                //              endif

                tlPostObject tempPostObject = null;

                if (cachedPage.posts != null)
                {
                    var matchingPost = (from w in cachedPage.posts
                                        where w.commentNumber == singlePostNumber
                                        select w);
                    tempPostObject = matchingPost.FirstOrDefault();
                }
                
                if (tempPostObject != null)
                {
                    tempPostObject.postContent = commentHTML;
                    if (singlePostNumber == postNumber)
                    {
                        returnString = commentHTML;
                        returnPost = tempPostObject;
                    }
                }
                else
                {
                    tempPostObject = new tlPostObject();
                    if (cachedPage.posts == null)
                    {
                        cachedPage.posts = new List<tlPostObject>();
                    }
                    cachedPage.posts.Add(tempPostObject);
                    tempPostObject.commentNumber = singlePostNumber;
                    tempPostObject.commentUri = new Uri(singlePostLink);
                    tempPostObject.postContent = commentHTML;
                    tempPostObject.threadSection = postSubforum;
                    tempPostObject.threadTitle = thread_title;
                    tempPostObject.uniqueThreadId = thread_id;
                    tempPostObject.threadStubUri = new Uri(ThreadStubStringFromThreadPageUri(new Uri(postLinkString)));
                    tempPostObject.Author = authorName;
                    tempPostObject.postDateTime = postDateTime;
                
                    if (singlePostNumber == postNumber)
                    {
                        returnString = commentHTML;
                        returnPost = tempPostObject;
                    }

                    if (authorName != null)
                    { 
                        var personMatch = (from x in tlPeople
                                           where x.tlName == authorName
                                           select x);
                        personObject tempPerson = personMatch.FirstOrDefault();

                        if (tempPerson != null && tempPerson.tlPostList != null)
                        {
                            tempPerson.tlPostList.Add(tempPostObject);
                        } else if (tempPerson != null && tempPerson.tlPostList == null)
                        {
                            //This now actually runs the first time a person's post gets added to their personObject.tlPostList
                            tempPerson.tlPostList = new List<tlPostObject>();
                            tempPerson.tlPostList.Add(tempPostObject);
                        }
                    }
                }

                readPosition = threadPage.IndexOf(startBlock, readPosition + commentBlock.Length);
            }

            //set the Next Page and Prev Page link Uri's here

            int currentPageNumber = 0;
            int postLinkPageStubIndex = postLinkString.IndexOf("?page=");
            int postLinkPageNumLoc = postLinkPageStubIndex + "?page=".Length;

            if (postLinkPageStubIndex == -1)
            {
                //This is the first page of the thread!
                currentPageNumber = 1;
                if (isThereANextPage)
                { 
                    cachedPage.nextThreadPage = new Uri(postLinkString + "?page=2");
                    if (postNumber > 18)
                    {
                        //Check to see if poat (20 x (2-1)) + 1 = 21 is cached
                        //If not, scrape the next page for post number 21
                        //I think I'm actually doing this check elsewhere. Review this block for culling.
                    }
                }
                else
                {
                    cachedPage.nextThreadPage = null;
                    //No next page. Scrape nothing.
                }
            }
            else
            {
                //Get the current page number
                string postLinkStringStub = postLinkString.Substring(0, postLinkPageStubIndex);

                currentPageNumber = Convert.ToInt32(postLinkString.Substring(postLinkPageNumLoc, postLinkString.Length - postLinkPageNumLoc));
                //Set the adjacent page links. If this is page 2, page 1 gets no ?page= extension.
                if (isThereANextPage)
                { 
                cachedPage.nextThreadPage = new Uri(postLinkStringStub + "?page=" + (currentPageNumber + 1).ToString());
                    if (postNumber > (20*(currentPageNumber - 1) + 18))
                    {
                        //Check to see if post (20 x (currentPageNumber)) + 1 is cached
                        //If not, scrape the next page for post number (20 x currentPageNumber) + 1)
                    }
                }
                else
                {
                    cachedPage.nextThreadPage = null;
                    //There isn't a next page, so don't scrape anything
                }

                if (currentPageNumber == 2)
                {
                    //Just the stub. Just to see how it feels.
                    cachedPage.prevThreadPage = new Uri(postLinkStringStub);
                    if (postNumber < 23)
                    {
                        //Check to see if post 20 is cached
                        //If not, scrape the previous page for post number 20
                    }
                }
                else if (currentPageNumber > 2)
                { 
                    //This page minus one.
                    cachedPage.prevThreadPage = new Uri(postLinkStringStub + "?page=" + (currentPageNumber - 1).ToString());
                    if (postNumber < (20*(currentPageNumber - 1) + 3))
                    {
                        //Check to see if post 20*(currentPageNumber - 1) is cached
                        //If not, scrape the previous page for post number 20*(currentPageNumber - 1)
                    }
                }

            }

            cachedPage.pageNumber = currentPageNumber;
            
            while (grabTlPostListTasks.Count() > 0)
            {
                Task firstProcessedList = await Task.WhenAny(grabTlPostListTasks);
                grabTlPostListTasks.Remove(firstProcessedList);
                //tlPostObject thisProcessedList = await firstProcessedList;
                //returnedPosts.Add(thisProcessedPost); //Not returning these at this point, because grabUsersTlPosts already processes them
            }

            if (returnPost.commentNumber == 0)
            {
                return null;
            }
            else
            {
            return returnPost;
            }
        }

        private static int threadIdFromThreadUriString(string postLinkString)
        {
            int thread_id_start = postLinkString.LastIndexOf("/") + 1;
            int thread_id_length = postLinkString.IndexOf("-", thread_id_start) - thread_id_start;
            int thread_id = Convert.ToInt32(postLinkString.Substring(thread_id_start, thread_id_length));
            return thread_id;
        }

        static string twitterNameFromURI(Uri twitterURI)
        {
            return HTMLUtilities.NameFromURI("Twiter Profile", "twitter.com/", twitterURI);
        }

        static string tlNameFromURI(Uri tlProfileURI)
        {
            return HTMLUtilities.NameFromURI("Teamliquid Profile", "teamliquid.net/forum/profile.php?user=", tlProfileURI);
        }

        static string fbNameFromURI(Uri fbProfileURI)
        {
            return HTMLUtilities.NameFromURI("Facebook Profile", "facebook.com/", fbProfileURI);
        }

        static string redditNameFromURI(Uri redditProfileURI)
        {
            return HTMLUtilities.NameFromURI("Reddit Profile", "reddit.com/user/", redditProfileURI);
        }

        private static void unfollowAndStopSerializing(string personToUnfollow, List<personObject> tlPeople, List<personObject> followedTLPeople, string fileName)
        {
            var personToUnfollowObjTest = (from u in tlPeople
                                       where (u.liquipediaName != null && u.liquipediaName.ToUpper() == personToUnfollow)
                                       || (u.tlName != null && u.tlName.ToUpper() == personToUnfollow)
                                       select u);
            personObject personToUnfollowObj = personToUnfollowObjTest.FirstOrDefault();
            if (personToUnfollowObj.Equals(null))
            {
                Console.WriteLine("Person not found!");
                return;
            }
            else
            {
                //Check to see if person already not being followed
                if (!personToUnfollowObj.followed)
                {
                    if (personToUnfollowObj.liquipediaName != null)
                    { 
                        Console.WriteLine("You're not even following " + personToUnfollowObj.liquipediaName + "!");
                    }
                    else if (personToUnfollowObj.tlName != null)
                    {
                        Console.WriteLine("You're not even following " + personToUnfollowObj.tlName + "!");
                    }
                }
                else
                {
                    personToUnfollowObj.followed = false;
                    followedTLPeople.Remove(personToUnfollowObj);
                    StopSerializing(fileName, personToUnfollowObj);
                    if (personToUnfollowObj.liquipediaName != null)
                    { 
                        Console.WriteLine("Successfully unfollowed " + personToUnfollowObj.liquipediaName);
                    }
                    else if (personToUnfollowObj.tlName != null)
                    {
                        Console.WriteLine("Successfully unfollowed " + personToUnfollowObj.tlName);
                    }
                }
                return;
            }
        }

        private static void StopSerializing(string fileName, personObject personToUnfollowObj)
        {
            FileStream s = new FileStream(fileName, FileMode.Open);
            IFormatter formatter = new BinaryFormatter();
            while (s.Position != s.Length)
            {
                long objStartPosition = s.Position;
                personObject v = (personObject)formatter.Deserialize(s);

                if ((v.liquipediaName == personToUnfollowObj.liquipediaName) || (v.tlName == personToUnfollowObj.tlName))
                {
                    long nextObjPosition = s.Position;
                    //Need to remove data from objStartPosition to (s.Position - 1). So, copy everything from s.Position to the end, and move is to objStartPosition, then truncate
                    long bytesToGrab = s.Length - s.Position;
                    int[] bytesLeft = new int[bytesToGrab];
                    while (s.Position != s.Length)
                    {
                        bytesLeft[s.Position - nextObjPosition] = s.ReadByte();
                    }

                    BinaryWriter bw = new BinaryWriter(s);
                    bw.Seek((int)objStartPosition, SeekOrigin.Begin);
                    for (int i = 0; i < bytesToGrab; i++)
                    {
                        bw.Write((byte)bytesLeft[i]);
                    }
                    s.SetLength(s.Position);
                    //Set the position equal to the end after you truncate the file; that way this while loop will exit
                }

            }
            s.Close();
        }



        private static async Task followAndSerialize(string personToFollow, List<personObject> tlPeople, List<personObject> followedTLPeople, string fileName)
        {
            var personToFollowObj = (from u in tlPeople
                                     where (u.liquipediaName != null && u.liquipediaName.ToUpper() == personToFollow)
                                     || (u.tlName != null && u.tlName.ToUpper() == personToFollow)
                                     select u);
            if (personToFollowObj.Count() != 1)
            {
                Console.WriteLine("Person not found in Liquipedia!");
                personObject tlUserPerson = await extractPersonDetail(personToFollow, tlPeople);
                if (tlUserPerson != null)
                {
                    Console.WriteLine("It's okay, found them in the TeamLiquid.net forums!");
                    await followAndSerialize(personToFollow, tlPeople, followedTLPeople, fileName);
                }
                else
                {
                    Console.WriteLine("Nobody by that name found in the TeamLiquid.net forums, either.");
                    return;
                }
            }
            else
            {
                //Check to see if already followed
                personObject followPersonObject = personToFollowObj.FirstOrDefault();
                if (followPersonObject.followed)
                {
                    if (followPersonObject.liquipediaName != null)
                    { 
                        Console.WriteLine("You're already following " + followPersonObject.liquipediaName + "!");
                    }
                    else if (followPersonObject.tlName != null)
                    {
                        Console.WriteLine("You're already following " + followPersonObject.tlName + "!");
                    }
                }
                else
                {
                    followPersonObject.followed = true;
                    followedTLPeople.Add(followPersonObject);
                    if (followPersonObject.liquipediaName != null)
                    {
                        await extractPersonDetail(followPersonObject, tlPeople);
                        Console.WriteLine("Following " + followPersonObject.liquipediaName);
                    }
                    else if (followPersonObject.tlName != null)
                    {
                        await extractPersonDetail(followPersonObject, tlPeople);
                        Console.WriteLine("Following " + followPersonObject.tlName);
                    }
                    StartSerializingPerson(fileName, followPersonObject);
                }
                return;
            }
        }

        private static void StartSerializingPerson(string fileName, personObject followPersonObject)
        {
            FileStream s = new FileStream(fileName, FileMode.Append);
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(s, followPersonObject);
            s.Close();
        }

        private static void DeserializeFollowedPlayers(string fileName, List<personObject> tlPeople, List<personObject> followedTLPeople)
        {
            if (File.Exists(fileName))
            {
                FileStream d = new FileStream(fileName, FileMode.Open);
                IFormatter formatter = new BinaryFormatter();
                if (d.Length != 0)
                {
                    while (d.Position != d.Length)
                    {
                        personObject t = (personObject)formatter.Deserialize(d);

                        //When you deserialize, you don't literally pull the object back; otherwise there would be two of each followed player.
                        //Instead, take the saved information from the serialized object and put it into the player in tlPeople.
                        //This is not true for TL forum-only people, as they aren't in tlPeople by default.
                        personObject personToFollowObj = null;
                        var personToFollowObjTest = (from u in tlPeople
                                                     where (u.liquipediaName != null) && (t.liquipediaName != null) && (u.liquipediaName.ToUpper() == t.liquipediaName.ToUpper())
                                                     select u);
                        personToFollowObj = personToFollowObjTest.FirstOrDefault();

                        if (t.liquipediaName == null)
                        {
                            if (t.tlName != null)
                            {
                                //Re=follow teamliquid.net account person
                                //You can either re-scrape their player page, or serialize their TL.net page
                                //Keep in mind you're going to serialize their number of posts anyway...
                                tlPeople.Add(t);
                                followedTLPeople.Add(t);
                                Console.WriteLine("Successfully followed " + t.tlName);
                            }
                            else
                            { 
                                Console.WriteLine("Person not found!");
                            }
                        }
                        else
                        {
                            if (personToFollowObj != null)
                            {
                                personToFollowObj.followed = true;
                                personToFollowObj.tlName = t.tlName;
                                personToFollowObj.tlForumURI = t.tlForumURI;
                                personToFollowObj.tlTotalPosts = t.tlTotalPosts;
                                followedTLPeople.Add(personToFollowObj);

                                if (personToFollowObj.liquipediaName != null)
                                { 
                                    Console.WriteLine("Successfully followed " + personToFollowObj.liquipediaName);
                                }
                                else if (personToFollowObj.tlName != null)
                                {
                                    Console.WriteLine("Successfully followed " + personToFollowObj.tlName);
                                }
                            }
                        }
                    }
                }
                d.Close();
            }
            else
            {
                FileStream d = new FileStream(fileName, FileMode.Create);
                d.Close();
            }
        }

        private static void StartSerializingCachedPage(string cachedPageFileName, tlCachedPostPage cachedPageObject)
        {
            FileStream s = new FileStream(cachedPageFileName, FileMode.Append);
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(s, cachedPageObject);
            s.Close();
        }

        private static void DeserializeCachedPages(string cachedfileName, List<tlCachedPostPage> cachedPages, List<personObject> tlPeople)
        {
            Console.WriteLine("Deserializing cached pages from the cached file.");
            if (File.Exists(cachedfileName))
            {
                FileStream d = new FileStream(cachedfileName, FileMode.Open);
                IFormatter formatter = new BinaryFormatter();
                if (d.Length != 0)
                {
                    while (d.Position != d.Length)
                    {
                        tlCachedPostPage t = (tlCachedPostPage)formatter.Deserialize(d);

                        cachedPages.Add(t);

                        foreach (tlPostObject u in t.posts)
                        {
                            if (u.Author != null)
                            {
                                var personMatch = (from x in tlPeople
                                                   where x.tlName == u.Author
                                                   select x);
                                personObject tempPerson = personMatch.FirstOrDefault();

                                if (tempPerson != null && tempPerson.tlPostList != null)
                                {
                                    tempPerson.tlPostList.Add(u);
                                }
                                else if (tempPerson != null && tempPerson.tlPostList == null)
                                {
                                    //This now actually runs the first time a person's post gets added to their personObject.tlPostList
                                    tempPerson.tlPostList = new List<tlPostObject>();
                                    tempPerson.tlPostList.Add(u);
                                }
                            }

                        }
                    }
                }
                d.Close();
                Console.WriteLine("Done derserializing");
            }
            else
            {
                FileStream d = new FileStream(cachedfileName, FileMode.Create);
                d.Close();
            }
        }

        private static void stopSerializingCachedPage(string cachedPageFileName, tlCachedPostPage pageToRemove) //For when an updated version of a page has been scraped
        {
            Console.WriteLine("Removing a cached page from the cache file.");
            FileStream s = new FileStream(cachedPageFileName, FileMode.Open);
            IFormatter formatter = new BinaryFormatter();
            while (s.Position != s.Length)
            {
                long objStartPosition = s.Position;
                tlCachedPostPage v = (tlCachedPostPage)formatter.Deserialize(s);

                if (v.cachedPageRemoteUri == pageToRemove.cachedPageRemoteUri)
                {
                    long nextObjPosition = s.Position;
                    //Need to remove data from objStartPosition to (s.Position - 1). So, copy everything from s.Position to the end, and move is to objStartPosition, then truncate
                    long bytesToGrab = s.Length - s.Position;
                    int[] bytesLeft = new int[bytesToGrab];
                    while (s.Position != s.Length)
                    {
                        bytesLeft[s.Position - nextObjPosition] = s.ReadByte();
                    }

                    BinaryWriter bw = new BinaryWriter(s);
                    bw.Seek((int)objStartPosition, SeekOrigin.Begin);
                    for (int i = 0; i < bytesToGrab; i++)
                    {
                        bw.Write((byte)bytesLeft[i]);
                    }
                    s.SetLength(s.Position);
                    //Set the position equal to the end after you truncate the file; that way this while loop will exit
                }

            }
            s.Close();
            Console.WriteLine("Done removing.");
        }

        static async Task ScrapeGlobalPlayerLists(List<personObject> tlPeople)
        {
            using (var client = new HttpClient())
            {
                //The "Continent" pages is where I get lists of names for the database to choose which players the user wants to follow
                Uri[] continentPagesURI = new Uri[4];
                continentPagesURI[0] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Europe)");
                continentPagesURI[1] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(US)");
                continentPagesURI[2] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Asia)");
                continentPagesURI[3] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Korea)");
                client.BaseAddress = continentPagesURI[0];
                
                foreach (Uri continentUri in continentPagesURI)
                {
                                      
                    var response = await client.GetAsync(continentUri);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        UTF8Encoding utf8 = new UTF8Encoding();
                        //string responseString = utf8.GetString(response);
                        string responseString = await response.Content.ReadAsStringAsync();
                        //var responseString = Encoding.UTF8.GetString(responseStringFromBytes, 0, response.Length - 1);
                        //These two commented rows are relics of my failed attempt at handling foreign-language characters
                        //encoded as UTF8. Apparently, the entire struggle was doomed because my console can't display UTF8
                        //characters with byte lengths longer than 8, even with the encoding set to UTF8.
                        //If I want to ensure UTF8 characters later (after adding a GUI,) I can ressurect these; I mey need to change the
                        //two operative lines to read from a Byte Array and convert it to a String using UTF8 encoding.
                        
                        int c = 0;
                        int countryStart = 0;
                        int countryEnd = 0;
                        int tableStart = 0;
                        int tableEnd = 0;
                        string tr_can_str = "<tr style=\"background-color:";
                        int tr_candidate = 0;
                        int tr_color_end = 0;
                        int tr_end = 0;
                        int td_start = 0;
                        int td_end = 0;
                        int td_length = 0;
                        string td_tags = "";
                        string td_info;
                        
                        try
                        {
                        
                            while (c < (responseString.Length - 40))
                            {
                            
                                countryStart = responseString.IndexOf("<h3><span class=\"mw-headline\" id=", c);
                                if (countryStart == -1)
                                    break;

                                countryEnd = responseString.IndexOf("</h3>", countryStart);

                                //InnerText() goes through each char in the responseString from start to end and does a Console.Write
                                //for every char that isn't nested in brackets (so everything that isn't HTML markup)
                                //No need to actually do the commented out line for country, as each Player comes with a country <TD> tag
                                //string countryName = InnerText(responseString, countryStart, countryEnd).Trim();
                                c = countryEnd + 4;

                                //Find the scope of the current country's table of players

                                tableStart = responseString.IndexOf("<table ", countryEnd);
                                tableEnd = responseString.IndexOf("<h3><span class=\"mw-headline\" id=", tableStart);

                                if (tableEnd == -1) tableEnd = responseString.Length; 

                                c = tableStart + 6;


                                //As it turns out, each country has more than one table (for e.g. retired players, casters, etc.,) so I changed
                                //"tableEnd" to the beginning of the next country; that way it just keeps reading <tr>s until it hits the next
                                //full country block of folks.
                                //I suppose I could skip the whole process and just read ever <tr> on the page at once... but that does make me
                                //nervous. This way I can control the stream a bit more if need be. For example, later I may want to keep track
                                //of which players are active, which are casters, which are retired, etc.

                                while (c < tableEnd)
                                {

                                    //Now, I need to look for every <tr bgcolor="(red, blue, green or yellow)"> until I run into the end of the Country; I can start from c because I already incemented it.
                                    //tr_candidate is the location of a tr bgcolor; I need to check to see if it is one of the right colors
                                    //Why the tr bgcolor? I'm glad you asked! Liquipedia colors the player table rows based on a player's race (Terran,
                                    //Zerg or Protoss,) so it's an easy way to discern whether a row is a player, or a header, or a bunch of blanks.
                                    //It will break if TL ever redesigns these pages, but then... what wouldn't break?

                                    tr_candidate = responseString.IndexOf(tr_can_str, c); //finds a <tr> with a bgcolor specified, which should be a player
                                    
                                    if (tr_candidate == -1)
                                    {
                                        //Console.WriteLine("No TR tag found");
                                        break;
                                    }else if (tr_candidate > tableEnd)
                                    {
                                        //Console.WriteLine("The next TR tag suprasses this table.");
                                        break;
                                    }
                                                                        
                                    tr_end = responseString.IndexOf("</tr>", tr_candidate);
                                    tr_color_end = responseString.IndexOf("\"", tr_candidate + tr_can_str.Length + 1);
                                    string colorCode = responseString.Substring(tr_candidate + tr_can_str.Length, tr_color_end - tr_candidate - tr_can_str.Length); //grabs just the color code

                                    if (colorCode.Equals("#B8B8F2;") //blue (Terran)
                                        || colorCode.Equals("#B8F2B8;") //green (Protoss)
                                        || colorCode.Equals("#F2B8B8;") //pink (Zerg)
                                        || colorCode.Equals("#F2E8B8;") //ugly tan color (Random?)
                                        || colorCode.Equals("rgb(184,242,184);")
                                        || colorCode.Equals("rgb(242,184,184);")
                                        || colorCode.Equals("rgb(184,184,242);")
                                        || colorCode.Equals("rgb(242,232,184);"))
                                    {
                                        //We've found a player TR! So grab the info out of each <td> (some may be empty!) and spill it to the player database
                                        //Creating a new person to put information into
                                        personObject tempPerson = new personObject();

                                        for (var i = 1; i <= 6; i++)
                                        {
                                            //There should be exactly 6 TDs; for now, cycle through them and use switch to assign data to properties.
                                            //If liquipedia changes the table, this (and everything else) will break
                                            td_start = HTMLUtilities.nextTDstart(responseString, tr_candidate);
                                            td_end = HTMLUtilities.nextTDend(responseString, tr_candidate);
                                            td_length = HTMLUtilities.nextTDlength(responseString, tr_candidate);

                                            //Td_tags is just the HTML code for this player; it is easier to inspect with WriteLine than the whole page 
                                            td_tags = responseString.Substring(td_start, td_length);
                                            //Remove the <span>...</span> sections that are duplicating some information (like team names)
                                            td_info = HTMLUtilities.removeTag(td_tags, "span");
                                            //Clip out all the HTML tag <...> substrings; leave just the content 
                                            td_info = HTMLUtilities.InnerText(td_info, 0, td_info.Length);
                                            
                                            if (td_info != null)
                                            {
                                                td_info = td_info.Trim();
                                                //Remove weird character codes like &#160;
                                                td_info = HTMLUtilities.removeCharCodes(td_info);
                                            
                                            
                                                //Assign the properties you are grabbing to the personObject
                                                switch (i)
                                                {
                                                    case 1:
                                                        //tempPerson.liquipediaName = HTMLUtilities.StringFromParameter(td_tags, "title");
                                                        tempPerson.liquipediaName = HTMLUtilities.InnerText(td_tags).Trim();
                                                        tempPerson.liquipediaURI = new Uri("http://wiki.teamliquid.net" + HTMLUtilities.grabHREF(td_tags));
                                                        break;
                                                    case 2:
                                                        tempPerson.irlName = td_info;
                                                        break;
                                                    case 3:
                                                        tempPerson.teamName = td_info;
                                                        break;
                                                    case 4:
                                                        tempPerson.country = td_info;
                                                        break;
                                                    case 5:
                                                        tempPerson.mainRace = td_info;
                                                        break;
                                                    case 6:
                                                        //This will grab twitch IDs, but will need to grab own3d IDs or, e.g. day9.tv
                                                        string hrefTag = HTMLUtilities.grabHREF(td_tags);
                                                        if (hrefTag != null)
                                                        {
                                                            //Trimming slashes and octothorps, because e.g. MarineKing/ and beastyqt#/
                                                            char[] trimChars = { '/', '#' };
                                                            tempPerson.twitchName = twitchIDfromURI(new Uri(hrefTag));
                                                            if (tempPerson.twitchName != null)
                                                            {
                                                                tempPerson.twitchName = tempPerson.twitchName.TrimEnd(trimChars);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            tempPerson.twitchName = null;
                                                        }
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                            //move the starting point to look for a new <tr> to the end of the last <td>
                                            tr_candidate = td_end;
                                        }
                                        //Write this tempPerson to the playerObject list
                                        tlPeople.Add(tempPerson);
                                    }
                                    else
                                    {
                                        //Should be all the "lightgrey;" useless table rows
                                    }
                                    //Move the starting point to look for a new table to the last <tr> end
                                    c = tr_end;
                                }
                            }
                        }catch(ArgumentOutOfRangeException)
                        {
                            Console.WriteLine("An index was out of range; responseString.length = " + responseString.Length.ToString()
                                + ", c = " + c.ToString()
                                + ", start = " + countryStart.ToString()
                                + ", end = " + countryEnd.ToString()
                                + ", tr_candidate = " + tr_candidate.ToString()
                                + ", tr_end = " + tr_end.ToString()
                                + ", td_start = " + td_start.ToString()
                                + ", td_end = " + td_end.ToString());
                            Console.ReadKey();
                        }
                    
                    }
                }
                  
            }
        
        }

        public static string twitchIDfromURI(Uri sourceUri)
        {
            string sourceUriString = sourceUri.ToString();
            int idStart = sourceUriString.IndexOf("twitch.tv/") + 10;
            int idEnd = new int();
            
            if (idStart != 9)
            {
                idEnd = sourceUriString.Length;
            }
            else idEnd = -1;

            int idLength = idEnd - idStart;

            if ((idStart != 9) && (idEnd != -1) && (idStart < idEnd))
            {
                return sourceUriString.Substring(idStart, idLength);
            }
            else return null;       
        }

        [Serializable()]
        public class personObject : ISerializable
        {
            //Create a new personObject with all details (but not content) to be scraped from various sources
            public personObject()
            {
                //Empty constructor required to compile.
            }

            /// <summary>
            /// Creates an object reference to a person who can be followed on various accounts
            /// </summary>
            /// <param name="liquipediaName">The person's unique Liquipedia page name, if one exists</param>
            /// <param name="liquipediaURI">A Uri object to the person's Liquipedia page, if one exists</param>
            /// <param name="country">The person's home country</param>
            /// <param name="bnetName">The person's battle.net user ID and character code</param>
            /// <param name="bnetProfileURI">The person's battle.net profile Uri</param>
            /// <param name="mainRace">The person's main race played: Terran, Zerg, Protoss or Random</param>
            /// <param name="teamName">The person's team name</param>
            /// <param name="teamSiteURI">A Uri object to the person's team's webpage</param>
            /// <param name="irlName">The person's real name in format First [Middle] Last</param>
            /// <param name="twitterName">The person's Twitter account name</param>
            /// <param name="twitterURI">A Uri object to the person's Twitter profile page</param>
            /// <param name="tlName">The person's TeamLiquid.net/forum username</param>
            /// <param name="tlProfileURI">A Uri object to the person's TeamLiquid.net/forum profile</param>
            /// <param name="tlTotalPosts">The total number of comments the person has posted to TeamLiquid.net</param>
            /// <param name="fbName">The person's Facebook Username</param>
            /// <param name="fbURI">A Uri object to the person's Facebook profile</param>
            /// <param name="twitchName">The person's Twitch.tv Username</param>
            /// <param name="twitchURI">A Uri object to the person's Twitch.tv channel</param>
            /// <param name="followed">True if the user is following this player, false if the user is not</param>
            /// <param name="tlPostList">An object reference to a list of tlPostObjects containing posts made by the person</param>
            public personObject(string liquipediaName,
                                Uri liquipediaURI,
                                string country,
                                string bnetName,
                                Uri bnetProfileURI,
                                string mainRace,
                                string teamName,
                                Uri teamSiteURI,
                                string irlName,
                                string twitterName,
                                Uri twitterURI,
                                string tlName,
                                Uri tlProfileURI,
                                int tlTotalPosts,
                                string fbName,
                                Uri fbURI,
                                string twitchName,
                                Uri twitchURI,
                                bool followed,
                                List<tlPostObject> tlPostList)
            {
                uniqueID = liquipediaName;
            }

            //A unique identifier for each person; since liquipedia can only have one page per player,
            //and that's what I'm using as a source to scrape potential players, I will use it as the
            //unique ID for now.
            private string uniqueID;
            public string liquipediaName
            {
                get { return uniqueID; }
                set { uniqueID = value;}
            }
            
            private Uri liquipediaURIvalue;
            public Uri liquipediaURI
            {
                get { return liquipediaURIvalue; }
                set { liquipediaURIvalue = value; }
            }

            private Uri tlForumURIvalue;
            public Uri tlForumURI
            {
                get { return tlForumURIvalue;}
                set { tlForumURIvalue = value;}
            }

            private string bnetNamevalue;
            public string bnetName
            {
                get { return bnetNamevalue; }
                set { bnetNamevalue = value; }
            }

            private Uri bnetProfileURIvalue;
            public Uri bnetProfileURI
            {
                get { return bnetProfileURIvalue; }
                set { bnetProfileURIvalue = value; }
            }

            private string mainRaceValue;
            public string mainRace
            {
                get { return mainRaceValue; }
                set { mainRaceValue = value; }
            }

            private string teamNamevalue;
            public string teamName
            {
                get { return teamNamevalue; }
                set { teamNamevalue = value; }
            }

            private Uri teamSiteURIvalue;
            public Uri teamSiteURI
            {
                get { return teamSiteURIvalue; }
                set { teamSiteURIvalue = value; }
            }

            private string irlNamevalue;
            public string irlName
            {
                get { return irlNamevalue; }
                set { irlNamevalue = value; }
            }

            private string twitterNamevalue;
            public string twitterName
            {
                get { return twitterNamevalue; }
                set { twitterNamevalue = value; }
            }

            private string countryvalue;
            public string country
            {
                get { return countryvalue; }
                set { countryvalue = value; }
            }

            private Uri twitterURIvalue;
            public Uri twitterURI
            {
                get { return twitterURIvalue; }
                set { twitterURIvalue = value; }
            }

            private string tlNamevalue;
            public string tlName
            {
                get { return tlNamevalue; }
                set { tlNamevalue = value; }
            }

            private int tlTotalPostsValue;
            public int tlTotalPosts
            {
                get { return tlTotalPostsValue; }
                set { tlTotalPostsValue = value; }
            }

            private Uri redditProfileURIValue;
            public Uri redditProfileURI
            {
                get { return redditProfileURIValue; }
                set { redditProfileURIValue = value; }
            }

            private string redditUsernameValue;
            public string redditUsername
            {
                get { return redditUsernameValue; }
                set { redditUsernameValue = value; }
            }

            private string fbNamevalue;
            public string fbName
            {
                get { return fbNamevalue; }
                set { fbNamevalue = value; }
            }

            private Uri fbURIvalue;
            public Uri fbURI
            {
                get { return fbURIvalue; }
                set { fbURIvalue = value; }
            }

            private string twitchNamevalue;
            public string twitchName
            {
                get { return twitchNamevalue; }
                set { twitchNamevalue = value; }
            }

            private Uri twitchURIvalue;
            public Uri twitchURI
            {
                get { return twitchURIvalue; }
                set { twitchURIvalue = value; }
            }

            private bool followedvalue;
            public bool followed
            {
                get { return followedvalue; }
                set { followedvalue = value;}
            }

            // Implement this method to serialize data. The method is called  
            // on serialization. 
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // Use the AddValue method to specify serialized values.
                info.AddValue("followed", followedvalue, typeof(bool));
                info.AddValue("liquipediaName", liquipediaName, typeof(string));
                info.AddValue("tlName", tlName, typeof(string));
                info.AddValue("tlForumURI", tlForumURI, typeof(Uri));
                info.AddValue("tlTotalPosts", tlTotalPosts, typeof(int));
            }

            // The special constructor is used to deserialize values. 
            public personObject(SerializationInfo info, StreamingContext context)
            {
                // Reset the property value using the GetValue method.
                followedvalue = (bool) info.GetValue("followed", typeof(bool));
                liquipediaName = (string)info.GetValue("liquipediaName", typeof(string));
                tlName = (string)info.GetValue("tlName", typeof(string));
                tlForumURI = (Uri)info.GetValue("tlForumURI", typeof(Uri));
                tlTotalPosts = (int)info.GetValue("tlTotalPosts", typeof(int));
            }

            public void displayPersonProperties()
            {
                if (this.liquipediaName != null) Console.WriteLine(this.liquipediaName);
                if (this.liquipediaURI != null) Console.WriteLine(this.liquipediaURI.ToString());
                if (this.tlName != null) Console.WriteLine(this.tlName);
                if (this.tlForumURI != null) Console.WriteLine(this.tlForumURI.ToString());
                if (this.tlTotalPosts != 0) Console.WriteLine(this.tlTotalPosts.ToString());
                if (this.irlName != null) Console.WriteLine(this.irlName);
                if (this.teamName != null) Console.WriteLine(this.teamName);
                if (this.country != null) Console.WriteLine(this.country);
                if (this.mainRace != null) Console.WriteLine(this.mainRace);
                if (this.twitchName != null) Console.WriteLine(this.twitchName);
                Console.WriteLine();
            }

            private List<tlPostObject> tlPostListValue;
            public List<tlPostObject> tlPostList
            {
                get { return tlPostListValue; }
                set { tlPostListValue = value;}
            }
        }
        
        [Serializable()]
        public class tlCachedPostPage  : ISerializable //A single cached post page; collected by Objects and in one master list
        {
            public tlCachedPostPage()
            {
                //Empty container required to compile
            }

            /// <summary>
            /// Creates an object reference to a Teamliquid.net forum HTML page that has been cached
            /// </summary>
            /// <param name="cachedPageUniqueThreadID">The integer thread identifier from the remote Uri</param>
            /// <param name="cachedPageRemoteUri">The original, remote Uri of the cached page</param>
            /// <param name="needsRefresh">True if the page needs to be updated, false if not</param>
            /// <param name="posts">A list object containing references to the tlPostObjects for every post on the cached page</param>
            /// <param name="nextThreadPage">A Uri object pointing to the next page in this cachedPage's thread, if any.</param>
            /// <param name="pageNumber">The page number of the cached page as it appeared in it's original thread</param>
            /// <param name="prevThreadPage">A Uri object pointing to the previous page in this cachedPage's thread, if any.</param>
            
            public tlCachedPostPage(int cachedPageUniqueThreadID,
                                    Uri cachedPageRemoteUri,
                                    bool needsRefresh,
                                    List<tlPostObject> posts,
                                    Uri prevThreadPage,
                                    int pageNumber,
                                    Uri nextThreadPage)
            {
                //No unique ID at this point... maybe some substring of the URL?
                UniqueID = cachedPageUniqueThreadID;
                cachedPageRemoteUriValue = cachedPageRemoteUri;
                needsRefreshValue = needsRefresh;
                prevThreadPageValue = prevThreadPage;
                pageNumberValue = pageNumber;
                nextThreadPageValue = nextThreadPage;
        }
            
            private int UniqueID;
            public int cachedPageUniqueThreadID
            {
                get { return UniqueID; }
                set { UniqueID = value; }
            }

            private Uri cachedPageRemoteUriValue;
            public Uri cachedPageRemoteUri
            {
                get { return cachedPageRemoteUriValue; }
                set { cachedPageRemoteUriValue = value; }
            }

            private bool needsRefreshValue;
            public bool needsRefresh
            {
                get { return needsRefreshValue; }
                set { needsRefreshValue = value; }
            }

            private List<tlPostObject> postsValue;
            public List<tlPostObject> posts
            {
                get { return postsValue; }
                set { postsValue = value; }
            }

            private Uri prevThreadPageValue;
            public Uri prevThreadPage
            {
                get { return prevThreadPageValue; }
                set { prevThreadPageValue = value; }
            }

            private int pageNumberValue;
            public int pageNumber
            {
                get { return pageNumberValue; }
                set { pageNumberValue = value; }
            }

            private Uri nextThreadPageValue;
            public Uri nextThreadPage
            {
                get { return nextThreadPageValue; }
                set { nextThreadPageValue = value; }
            }

            // Implement this method to serialize data. The method is called  
            // on serialization. 
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // Use the AddValue method to specify serialized values.
                info.AddValue("UniqueID", UniqueID, typeof(int));
                info.AddValue("cachedPageRemoteUri", cachedPageRemoteUri, typeof(Uri));
                info.AddValue("needsRefresh", needsRefresh, typeof(bool));
                info.AddValue("posts", posts, typeof(List<tlPostObject>));
                info.AddValue("prevThreadPage", prevThreadPage, typeof(Uri));
                info.AddValue("pageNumber", pageNumber, typeof(int));
                info.AddValue("nextThreadPage", nextThreadPage, typeof(Uri));
            }

            // The special constructor is used to deserialize values. 
            public tlCachedPostPage(SerializationInfo info, StreamingContext context)
            {
                // Reset the property value using the GetValue method.
                UniqueID = (int)info.GetValue("UniqueID", typeof(int));
                cachedPageRemoteUri = (Uri)info.GetValue("cachedPageRemoteUri", typeof(Uri));
                needsRefresh = (bool)info.GetValue("needsRefresh", typeof(bool));
                posts = (List<tlPostObject>)info.GetValue("posts", typeof(List<tlPostObject>));
                prevThreadPage = (Uri)info.GetValue("prevThreadPage", typeof(Uri));
                pageNumber = (int)info.GetValue("pageNumber", typeof(int));
                nextThreadPage = (Uri)info.GetValue("nextThreadPage", typeof(Uri));
            }
        }
    
        [Serializable()]
        public class tlPostObject : ISerializable
        {
            public tlPostObject()
            {
                //Empty container required to compile
            }

            /// <summary>
            /// Creates an object reference to a specific Comment or Post on TeamLiquid.
            /// </summary>
            /// <param name="uniqueThreadId">The unique integer identifier tied to the TeamLiquid.net/forum thread in which this Post or Comment appears</param>
            /// <param name="threadStubUri">A Uri object pointing to the first page of the thread (so, everything before "?page=")</param>
            /// <param name="threadTitle">The string title of the thread from the TeamLiquid.net forums in which this Post or Comment appears</param>
            /// <param name="threadSection">The Teamliquid.net sub-forum on which the thread for this Post or Comment appears</param>
            /// <param name="commentUri">The Uri address object linking directly to this Post or Comment</param>
            /// <param name="commentNumber">The <![CDATA[ <a name=#> ]]> comment number of this Post or Comment in its parent thread</param>
            /// <param name="postHTMLContent">A string containing the HTML markup content of the Post or Comment</param>
            /// <param name="postDateTime">The Date and Time the post was made.</param>
            /// <param name="Author">The author of the post, if any</param>
            public tlPostObject(int uniqueThreadId,
                                Uri threadStubUri,
                                string threadTitle,
                                string threadSection,
                                Uri commentUri,
                                int commentNumber,
                                string postHTMLContent,
                                string postDateTime,
                                //CachePageObject (will be shared with all other postObjects on the same page)
                                string Author
                                )
            {
                UniqueID = uniqueThreadId;   
            }

            private int UniqueID;
            public int uniqueThreadId
            {
                get { return UniqueID; }
                set { UniqueID = value;}
            }

            private Uri threadStubUriValue;
            public Uri threadStubUri
            {
                get { return threadStubUriValue; }
                set { threadStubUriValue = value; }
            }

            private string threadTitleValue;
            public string threadTitle
            {
                get { return threadTitleValue; }
                set { threadTitleValue = value; }
            }

            private string threadSectionValue;
            public string threadSection
            {
                get { return threadSectionValue; }
                set { threadSectionValue = value; }
            }

            private Uri commentUriValue;
            public Uri commentUri
            {
                get { return commentUriValue; }
                set { commentUriValue = value; }
            }

            private int commentNumberValue;
            public int commentNumber
            {
                get { return commentNumberValue; }
                set { commentNumberValue = value; }
            }

            private string postContentValue;
            public string postContent
            {
                get { return postContentValue; }
                set { postContentValue = value; }
            }

            private string postDateTimeValue;
            public string postDateTime
            {
                get { return postDateTimeValue; }
                set { postDateTimeValue = value; }
            }

            private string postAuthorValue;
            public string Author
            {
                get { return postAuthorValue; }
                set { postAuthorValue = value; }
            }

            //int uniqueThreadId,
            //Uri threadStubUri,
            //string threadTitle,
            //string threadSection,
            //Uri commentUri,
            //int commentNumber,
            //string postHTMLContent,
            //String postDateTime,
            //string Author

            // Implement this method to serialize data. The method is called  
            // on serialization. 
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // Use the AddValue method to specify serialized values.
                info.AddValue("uniqueThreadId", uniqueThreadId, typeof(int));
                info.AddValue("threadStubUri", threadStubUri, typeof(Uri));
                info.AddValue("threadTitle", threadTitle, typeof(string));
                info.AddValue("threadSection", threadSection, typeof(string));
                info.AddValue("commentUri", commentUri, typeof(Uri));
                info.AddValue("commentNumber", commentNumber, typeof(int));
                info.AddValue("postContent", postContent, typeof(string));
                info.AddValue("postDateTime", postDateTime, typeof(string));
                info.AddValue("Author", Author, typeof(string));
            }

            // The special constructor is used to deserialize values. 
            public tlPostObject(SerializationInfo info, StreamingContext context)
            {
                // Reset the property value using the GetValue method.
                uniqueThreadId = (int)info.GetValue("uniqueThreadId", typeof(int));
                threadStubUri = (Uri)info.GetValue("threadStubUri", typeof(Uri));
                threadTitle = (string)info.GetValue("threadTitle", typeof(string));
                threadSection = (string)info.GetValue("threadSection", typeof(string));
                commentUri = (Uri)info.GetValue("commentUri", typeof(Uri));
                commentNumber = (int)info.GetValue("commentNumber", typeof(int));
                postContent = (string)info.GetValue("postContent", typeof(string));
                postDateTime = (String)info.GetValue("postDateTime", typeof(String));
                Author = (string)info.GetValue("Author", typeof(string));
            }
        }
    }
}
