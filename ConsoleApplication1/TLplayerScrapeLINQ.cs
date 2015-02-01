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
            string fileName = "dataStuff.myData";
            

            List<personObject> tlPeople = new List<personObject>();
            
            //Most of the functionality is hidden in RunAsync right now. It grabs all the web data.
            RunAsync(tlPeople).Wait();
            
            //Default ordering is by Liquipedia Name
            tlPeople = tlPeople.OrderBy(o => o.liquipediaName).ToList();

            Console.WriteLine("Done! " + tlPeople.Count.ToString() + " players found!");

            DeserializeFollowedPlayers(fileName, tlPeople); //Take players from "filename" and put them into List tlPeople (or create fileName if it doesn't exist)
            
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
                        Console.WriteLine("Type the Liquipedia Name of the person to follow:");
                        Console.WriteLine();
                        string personToFollow = Console.ReadLine().ToUpper();
                        followAndSerialize(personToFollow, tlPeople, fileName);
                        break;
                    case "C":
                        Console.WriteLine("Type the Liquipedia Name of the person to unfollow:");
                        Console.WriteLine();
                        string personToUnfollow = Console.ReadLine().ToUpper();
                        unfollowAndStopSerializing(personToUnfollow, tlPeople, fileName);
                        break;
                    case "D":
                        foreach (personObject person in tlPeople)
                        {
                            person.displayPersonProperties();
                        }
                        break;
                    case "E":
                        var listOfFollowed = (from v in tlPeople
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
                    case "Q":
                        quitThisGame = 1;
                        break;
                    default:
                        break;
                }
                Console.WriteLine();
            }


        }

        static personObject personObjectFromString(string personString, List<personObject> tlPeople)
        {
            var person = (from u in tlPeople
                          where u.liquipediaName.ToUpper() == personString.ToUpper()
                          select u);
            if (person.FirstOrDefault().Equals(null))
            {
                Console.WriteLine("Person not found!");
                return null;
            }
            else
            {
                return person.FirstOrDefault();
            }
        }

        static async Task extractPersonDetail(string personForDetailView, List<personObject> tlPeople)
        {
            personObject person = personObjectFromString(personForDetailView, tlPeople);

            //1. Load async the players teamliquid.net profile URL
            using (var client = new HttpClient())
            {
                Uri playerDetailUri = new Uri(person.liquipediaURI);
                    
                var response = await client.GetAsync(playerDetailUri);

                if (response.IsSuccessStatusCode)
                {
                    UTF8Encoding utf8 = new UTF8Encoding();
                    //string responseString = utf8.GetString(response);
                    string responseString = await response.Content.ReadAsStringAsync();

                    string infoBox_tags = StringFromTag(responseString, "<div class=\"infobox-center infobox-icons\">", "</div>");

                    //2. Scrape that page for the rest of the detail properties
                    //3. Fill those (switch like the main list scraper)

                    person.tlForumURI = hrefFromTitle(infoBox_tags, "TeamLiquid.net Profile");
                    person.tlName = tlNameFromURI(person.tlForumURI);
                    person.twitterURI = hrefFromTitle(infoBox_tags, "Twitter");
                    person.twitterName = twitterNameFromURI(person.twitterURI);
                    person.fbURI = hrefFromTitle(infoBox_tags, "Facebook");
                    person.fbName = fbNameFromURI(person.fbURI);
                    person.twitchURI = hrefFromTitle(infoBox_tags, "Twitch Stream");
                    person.twitchName = twitchIDfromURI(person.twitchURI);
                    person.redditProfileURI = hrefFromTitle(infoBox_tags, "Reddit Profile");
                    person.redditUsername = redditNameFromURI(person.redditProfileURI);

                    //There are some other tags, e.g., battle.net urls, that show up later under "external links"
                    //Since I really want to move on to pulling the posts from TL, I'm putting off grabbing that
                    //stuff until later.
                }

                    
            }
            //4. Display the details (will ultimately return)
            Console.WriteLine(person.tlName + " on teamliquid: " + person.tlForumURI);
            Console.WriteLine(person.twitterName + " on Twitter: " + person.twitterURI);
            Console.WriteLine(person.fbName + " on Facebook: " + person.fbURI);
            Console.WriteLine(person.twitchName + " on Twitch.tv: " + person.twitchURI); //updates the one scraped from the countries list, for uniformity
            Console.WriteLine(person.redditUsername + " on Reddit: " + person.redditProfileURI);
            
        }

        static string NameFromURI(string NameIdentifier, string URIStub, string fullURI)
        {
            int URI_index = fullURI.IndexOf(URIStub);
            if (URI_index == -1)
            {
                return "Name not found. Are you sure this is a " + NameIdentifier + "URI?";
            }
            else
            {
                int name_index = URI_index + URIStub.Length;
                int name_length = fullURI.Length - name_index;
                return fullURI.Substring(name_index, name_length);
            }
        }

        static string twitterNameFromURI(string twitterURI)
        {
            return NameFromURI("Twiter Profile", "twitter.com/", twitterURI);
        }

        static string tlNameFromURI(string tlProfileURI)
        {
            return NameFromURI("Teamliquid Profile", "teamliquid.net/forum/profile.php?user=", tlProfileURI);
        }

        static string fbNameFromURI(string fbProfileURI)
        {
            return NameFromURI("Facebook Profile", "facebook.com/", fbProfileURI);
        }

        static string redditNameFromURI(string redditProfileURI)
        {
            return NameFromURI("Reddit Profile", "reddit.com/user/", redditProfileURI);
        }

        public static string StringFromTag(string sourceString, string tagStart, string tagClose)
        {
            int tagStart_index = sourceString.IndexOf(tagStart);
            if (tagStart_index == -1)
            {
                return "Opening tag not found!";
            }
            else
            {
                int tagEnd_index = sourceString.IndexOf(tagClose, tagStart_index) + tagClose.Length;
                if (tagEnd_index == -1)
                {
                    return "Closing tag not found! (Did you close a different tag or is the HTML malformed?";
                }
                else
                {
                    int tag_length = tagEnd_index - tagStart_index;
                    return sourceString.Substring(tagStart_index, tag_length);
                }
            }
        }

        public static string hrefFromTitle(string sourceString, string title_name)
        {
            int title_index = sourceString.IndexOf("title=\"" + title_name + "\"");
            if (title_index != -1)
            {
                int tag_index = sourceString.LastIndexOf("<a href=\"", title_index);
                int href_start = sourceString.IndexOf("\"", tag_index) + 1;
                int href_end = sourceString.IndexOf("\"", href_start);
                int href_length = href_end - href_start;
                return sourceString.Substring(href_start, href_length);
            }
            else
            {
                return ("No " + title_name + " found!");
            }
        }

        private static void unfollowAndStopSerializing(string personToUnfollow, List<personObject> tlPeople, string fileName)
        {
            var personToUnfollowObj = (from u in tlPeople
                                       where u.liquipediaName.ToUpper() == personToUnfollow
                                       select u);
            if (personToUnfollowObj.FirstOrDefault().Equals(null))
            {
                Console.WriteLine("Person not found!");
                return;
            }
            else
            {
                //Check to see if person already not being followed
                if (!personToUnfollowObj.FirstOrDefault().followed)
                {
                    Console.WriteLine("You're not even following " + personToUnfollowObj.FirstOrDefault().liquipediaName + "!");
                }
                else
                {
                    personToUnfollowObj.FirstOrDefault().followed = false;
                    FileStream s = new FileStream(fileName, FileMode.Open);
                    IFormatter formatter = new BinaryFormatter();
                    while (s.Position != s.Length)
                    {
                        long objStartPosition = s.Position;
                        personObject v = (personObject)formatter.Deserialize(s);

                        if (v.liquipediaName == personToUnfollowObj.FirstOrDefault().liquipediaName)
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
                    Console.WriteLine("Successfully unfollowed " + personToUnfollowObj.First().liquipediaName);
                }
                return;
            }
        }

        private static void followAndSerialize(string personToFollow, List<personObject> tlPeople, string fileName)
        {
            var personToFollowObj = (from u in tlPeople
                                     where u.liquipediaName.ToUpper() == personToFollow
                                     select u);
            if (personToFollowObj.Count() != 1)
            {
                Console.WriteLine("Person not found!");
                return;
            }
            else
            {
                //Check to see if already followed
                if (personToFollowObj.FirstOrDefault().followed)
                {
                    Console.WriteLine("You're already following " + personToFollowObj.FirstOrDefault().liquipediaName + "!");
                }
                else
                {
                    personToFollowObj.FirstOrDefault().followed = true;
                    FileStream s = new FileStream(fileName, FileMode.Append);
                    IFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(s, personToFollowObj.FirstOrDefault());
                    s.Close();
                    Console.WriteLine("Successfully followed " + personToFollowObj.First().liquipediaName);
                }
                return;
            }
        }

        private static void DeserializeFollowedPlayers(string fileName, List<personObject> tlPeople)
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

                        var personToFollowObj = (from u in tlPeople
                                                 where u.liquipediaName.ToUpper() == t.liquipediaName.ToUpper()
                                                 select u);
                        if (personToFollowObj.Count() != 1)
                        {
                            Console.WriteLine("Person not found!");
                        }
                        else
                        {
                            personToFollowObj.FirstOrDefault().followed = true;
                            Console.WriteLine("Successfully followed " + personToFollowObj.First().liquipediaName);
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

        static async Task RunAsync(List<personObject> tlPeople)
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
                        int tr_candidate = 0;
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
                                    
                                    tr_candidate = responseString.IndexOf("<tr bgcolor=", c); //finds a <tr> with a bgcolor specified, which should be a player
                                    
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
                                    string colorCode = responseString.Substring(tr_candidate + 13, 7); //grabs just the 7-character color code

                                    if (colorCode.Equals("#B8B8F2") //blue (Terran)
                                        || colorCode.Equals("#B8F2B8") //green (Protoss)
                                        || colorCode.Equals("#F2B8B8") //pink (Zerg)
                                        || colorCode.Equals("#F2E8B8")) //ugly tan color (Random?)
                                    {
                                        //We've found a player TR! So grab the info out of each <td> (some may be empty!) and spill it to the player database
                                        //Creating a new person to put information into
                                        personObject tempPerson = new personObject();

                                        for (var i = 1; i <= 6; i++)
                                        {
                                            //There should be exactly 6 TDs; for now, cycle through them and use switch to assign data to properties.
                                            //If liquipedia changes the table, this (and everything else) will break
                                            td_start = nextTDstart(responseString, tr_candidate);
                                            td_end = nextTDend(responseString, tr_candidate);
                                            td_length = nextTDlength(responseString, tr_candidate);

                                            //Td_tags is just the HTML code for this player; it is easier to inspect with WriteLine than the whole page 
                                            td_tags = responseString.Substring(td_start, td_length);
                                            //Remove the <span>...</span> sections that are duplicating some information (like team names)
                                            td_info = removeTag(td_tags, "span");
                                            //Clip out all the HTML tag <...> substrings; leave just the content 
                                            td_info = InnerText(td_info, 0, td_info.Length).Trim();
                                            //Remove weird character codes like &#160;
                                            td_info = removeCharCodes(td_info);

                                            //Assign the properties you are grabbing to the personObject
                                            switch (i)
                                            {
                                                case 1:
                                                    tempPerson.liquipediaName = td_info;
                                                    tempPerson.liquipediaURI = "http://wiki.teamliquid.net" + grabHREF(td_tags);
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
                                                    //I also noticed MarineKing's twitch has an extra slash. Not sure why
                                                    tempPerson.twitchName = twitchIDfromURI(grabHREF(td_tags));
                                                    break;
                                                default:
                                                    //Console.WriteLine("Oh Gawd. Something has gone horribly wrong. i = " + i.ToString());
                                                    // (It really has, code execution should never reach this)
                                                    //Console.ReadKey();
                                                    break;
                                            }
                                            //move the starting point to look for a new <tr> to the end of the last <td>
                                            tr_candidate = td_end;
                                        }
                                        //Write this tempPerson to the playerObject list
                                        tlPeople.Add(tempPerson);
                                    }
                                    //Move the starting point to look for a new table to the last <tr> end
                                    c = tr_end;
                                }
                            }
                        }catch(ArgumentOutOfRangeException)
                        {
                            Console.WriteLine("Index is out of range; responseString.length = " + responseString.Length.ToString()
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


        private static string InnerText(string inputHTML, int start, int end)
        {
            int nesting = 0;
            string nestString = "<";
            string unnestString = ">";
            string innerTextString = "";
            string oneCharacter;
            
            //This is potentially confusing, because I call it "nesting" when it's really just keeping track of brackets,
            // and not nested brackets (e.g. table brackets). I think I could just start capturing after a ">"
            // until I reach a "<" without actually keeping track of the nesting... but leaving it as is for now.

            for (int i = start; i < end; i++)
            {
                oneCharacter = inputHTML[i].ToString();
                if (oneCharacter.Equals(nestString))
                {
                    nesting++;
                }
                else if (oneCharacter.Equals(unnestString))
                {
                    nesting--;
                }

                if ((nesting == 0) && !(oneCharacter.Equals("<")) && !(oneCharacter.Equals(">")))
                {
                    innerTextString = innerTextString + oneCharacter;
                }
            }

            innerTextString = innerTextString.Trim();

            if (innerTextString.Length == 0)
            {
                return "No Matching Text found";
            } else
            return innerTextString;
        }
        
        public static string removeCharCodes(string inputString)
        {
            return inputString.Replace("&#160;","");
        }

        public static int nextTDstart(string searchString, int startPosition)
        {
            return searchString.IndexOf("<td", startPosition);
        }

        public static int nextTDend(string searchString, int startPosition)
        {
            return (searchString.IndexOf("</td>", startPosition) + 5);
        }

        public static int nextTDlength(string searchString, int startPosition)
        {
            return (nextTDend(searchString, startPosition) - nextTDstart(searchString, startPosition));
        }

        public static string removeTag(string sourceString, string tagToRemove)
        {
            string startTagString = "<" + tagToRemove;
            string endTagString = "</" + tagToRemove + ">";
            int startTag = sourceString.IndexOf(startTagString);
            int endTag = (sourceString.IndexOf(endTagString) + endTagString.Length);

            if ((startTag != -1) && (endTag != -1) && (startTag < endTag))
            {
                int removeLength = endTag - startTag;
                return sourceString.Remove(startTag, removeLength);
            }
            else return sourceString;
        }

        public static string grabHREF(string sourceString)
        {
            int hrefLocation = sourceString.IndexOf("href");
            int urlStart = new int();

            if (hrefLocation != -1)
            {
                urlStart = sourceString.IndexOf("\"", hrefLocation) + 1;
            }
            else urlStart = 0;

            int urlEnd = new int();

            if (urlStart != 0)
            {
                urlEnd = sourceString.IndexOf("\"", urlStart);
            }
            else urlEnd = -1;

            int urlLength = urlEnd - urlStart;

            if ((hrefLocation != -1) && (urlStart != 0) && (urlEnd != -1) && (urlStart < urlEnd))
            {
                return sourceString.Substring(urlStart, urlLength);
            }
            else return "No link tags found";
        }

        public static string twitchIDfromURI(string sourceString)
        {
            int idStart = sourceString.IndexOf("twitch.tv/") + 10;
            int idEnd = new int();
            
            if (idStart != 9)
            {
                idEnd = sourceString.Length;
            }
            else idEnd = -1;

            int idLength = idEnd - idStart;

            if ((idStart != 9) && (idEnd != -1) && (idStart < idEnd))
            {
                return sourceString.Substring(idStart, idLength);
            }
            else return "No Twitch ID found";       
        }

        [Serializable()]
        public class personObject : ISerializable
        {
            //Create a new personObject with all details (but not content) to be scraped from various sources
            public personObject()
            {
                //Empty constructor required to compile.
            }

            public personObject(
                string liquipediaName,
                string liquipediaURI,
                string bnetName,
                string bnetProfileURI,
                string mainRace,
                string teamName,
                string teamSiteURI,
                string irlName,
                string twitterName,
                string country,
                string twitterURI,
                string tlName,
                string tlProfileURI,
                string fbName,
                string fbURI,
                string twitchName,
                string twitchURI,
                bool followed
                )
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
            
            private string liquipediaURIvalue;
            public string liquipediaURI
            {
                get { return liquipediaURIvalue; }
                set { liquipediaURIvalue = value; }
            }

            private string tlForumURIvalue;
            public string tlForumURI
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

            private string bnetProfileURIvalue;
            public string bnetProfileURI
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

            private string teamSiteURIvalue;
            public string teamSiteURI
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

            private string twitterURIvalue;
            public string twitterURI
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

            private string redditProfileURIValue;
            public string redditProfileURI
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

            private string fbURIvalue;
            public string fbURI
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

            private string twitchURIvalue;
            public string twitchURI
            {
                get { return twitchURIvalue; }
                set { twitchURIvalue = value; }
            }

            //Serializing only this property and the name
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

            }

            // The special constructor is used to deserialize values. 
            public personObject(SerializationInfo info, StreamingContext context)
            {
                // Reset the property value using the GetValue method.
                followedvalue = (bool) info.GetValue("followed", typeof(bool));
                liquipediaName = (string)info.GetValue("liquipediaName", typeof(string));
            }

            public void displayPersonProperties()
            {
                Console.WriteLine(this.liquipediaName);
                Console.WriteLine(this.liquipediaURI);
                Console.WriteLine(this.irlName);
                Console.WriteLine(this.teamName);
                Console.WriteLine(this.country);
                Console.WriteLine(this.mainRace);
                Console.WriteLine(this.twitchName);
                Console.WriteLine();
            }
        }
    }
}
