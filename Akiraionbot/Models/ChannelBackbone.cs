using Akiraionbot.Enums;
using Akiraionbot.Events;
using Akiraionbot.Models.TwitchAPI.Helix;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Akiraionbot.Models
{
    class ChannelBackBone
    {
        public string channel { get; set; }
        public ListView chatLV { get; set; }
        public ListView notificationLV { get; set; }
        public ConcurrentDictionary<string, User> chatUsers { get; set; }
        public ConcurrentDictionary<string, User> recentUser { get; set; }
        public ConcurrentDictionary<string, User> historicalFollows { get; set; }
        public int recentSize { get; set; }
        public User Last { get; set; }
        public User First { get; set; }
        public string lastPerson { get; set; }

        public FollowerCache followsCache { get; set; }

        public event EventHandler<OnDBActionArgs> onDBActionArgs; 

        public ConcurrentDictionary<string, Hero> heroList { get; set; }
        public string currentHero { get; set; }
        private Stopwatch cacheWatch { get; set; }
        private Hero cacheHero { get; set; }
        public string lastHero { get; set; }

        //twice the death duration with 5 seconds of cushion
        private int timetoCheck = 25;

        public int pointValue { get; set; } = 1;

        /// <summary>
        /// Setup the ChannelBackBone for a specified channel. chatUsers keeps track of all current
        /// Twitch users in the channel. recentUsers will hold information of the last 50 or x amount
        /// of users who have visited the channel but have not followed. historicalFollows will have
        /// users who are not in the channel but have been in the channel previously while this bot
        /// was running. followCache has the last 50 or x amount of users that followed the channel
        /// to compare against. heroList will keep track of all Overwatch heroes the tracking channel
        /// has played.
        /// </summary>
        /// <param name="channel">Specified channel</param>
        public ChannelBackBone(string channel)
        {
            this.channel = channel;
            chatLV = new ListView();
            notificationLV = new ListView();
            chatUsers = new ConcurrentDictionary<string, User>();
            recentUser = new ConcurrentDictionary<string, User>();
            historicalFollows = new ConcurrentDictionary<string, User>();
            followsCache = new FollowerCache();
            recentSize = 50;
            lastPerson = "";
            heroList = new ConcurrentDictionary<string, Hero>();
            currentHero = "";
            lastHero = "";
        }

        /// <summary>
        /// Add user to chatUsers with update logic
        /// </summary>
        /// <param name="theUser">Referenced user object</param>
        /// <param name="returnedUser">Updated or the same user object</param>
        /// <returns>True if added. False if added but after updating.</returns>
        public bool addUser(User theUser, out User returnedUser)
        {
            if (!retrieveRecentUser(theUser, out theUser))
            {
                retrieveFollowerUser(theUser, out theUser);
            }

            User tempUser;
            if (chatUsers.TryGetValue(theUser.name, out tempUser))
            {
                User buffUser;
                bool updated = User.lazyUpdate(tempUser, theUser, out buffUser);
                if (updated) theUser = buffUser;
                chatUsers.TryUpdate(theUser.name, theUser, tempUser);
                returnedUser = theUser;
                return false;
            }
            else
            {
                chatUsers.TryAdd(theUser.name, theUser);
                returnedUser = theUser;
                return true;
            }
        }

        /// <summary>
        /// Remove user from chatUsers and place in historicalFollows or recentUsers.
        /// </summary>
        /// <param name="username"></param>
        /// <returns>The user object that was removed from chatUsers</returns>
        public User removeActiveUser(string username)
        {
            if (chatUsers.ContainsKey(username))
            {
                User removedUser;
                bool tryRemoved = chatUsers.TryRemove(username, out removedUser);
                if (tryRemoved)
                {
                    removedUser.lastCheckIn = DateTime.Now;
                    User buffUser;
                    if (removedUser.followed_at != null)
                    {
                        buffUser = addFollowUser(removedUser);
                    }
                    else
                    {
                        buffUser = addRecentUser(removedUser);
                    }
                    return buffUser;
                }
                else
                {
                    Debug.WriteLine("Detected key at removeActiveUser but was unable to remove the key and value pair.");
                }
            }
            return null;
        }

        /// <summary>
        /// Attempt to remove user from recentUsers.
        /// </summary>
        /// <param name="theUser">Referenced user object</param>
        /// <param name="returnedUser">The user that was found. Null if User was not found.</param>
        /// <returns>True if user was found to return. Otherwise false.</returns>
        public bool retrieveRecentUser(User theUser, out User returnedUser)
        {
            if (recentUser.TryRemove(theUser.name, out returnedUser))
            {
                string zForward = returnedUser.Forward;
                string zBack = returnedUser.Back;
                if (zForward != "" && zBack != "")
                {
                    recentUser[zForward].Back = zBack;
                    recentUser[zBack].Forward = zForward;
                }
                else if (zForward != "" && zBack == "")
                {
                    recentUser[zForward].Back = "";
                    Last = recentUser[zForward];
                }
                else if (zForward == "" && zBack != "")
                {
                    recentUser[zBack].Forward = "";
                    Last = recentUser[zBack];
                }
                User.lazyUpdate(returnedUser, theUser, out returnedUser);
                return true;
            }
            returnedUser = theUser;
            return false;
        }

        /// <summary>
        /// Attempt to remove user from historicalFollows.
        /// </summary>
        /// <param name="theUser">Referenced user object</param>
        /// <param name="returnedUser">The user that was found. Null if user was notn found.</param>
        /// <returns></returns>
        public bool retrieveFollowerUser(User theUser, out User returnedUser)
        {
            User tempUser;
            if (historicalFollows.TryRemove(theUser.name, out tempUser))
            {
                User.lazyUpdate(tempUser, theUser, out returnedUser);
                return true;
            }
            returnedUser = theUser;
            return false;
        }

        /// <summary>
        /// Attempt to return copy of user object from chatUsers.
        /// </summary>
        /// <param name="theUser">Referenced username or login</param>
        /// <returns>The user object to return. Null if not found.</returns>
        public User returnUser(string theUser)
        {
            User queryUser;
            chatUsers.TryGetValue(theUser, out queryUser);
            return queryUser;
        }

        /// <summary>
        /// Checks chatUsers for specified user.
        /// </summary>
        /// <param name="theUser">Specified user</param>
        /// <returns>True if user was found.</returns>
        public bool containsUser(string theUser)
        {
            if (chatUsers.ContainsKey(theUser)) return true;
            return false;
        }

        /// <summary>
        /// recentUsers add logic
        /// </summary>
        /// <param name="theUser">Referenced user object</param>
        /// <returns>Updated(?) referenced user object</returns>
        public User addRecentUser(User theUser)
        {
            User tempUser;
            if (recentUser.TryGetValue(theUser.name, out tempUser))
            {
                User.lazyUpdate(tempUser, theUser, out tempUser);
                recentUser.TryUpdate(tempUser.name, tempUser, recentUser[tempUser.name]);
                theUser = tempUser;
            }
            else
            {
                recentUser.TryAdd(theUser.name, theUser);
            }
            if (recentUser.Count == 1)
            {
                First = theUser;
                Last = theUser;
            }
            else Last = theUser;
            if (recentUser.Count > 1)
            {
                theUser.Forward = Last.name;
                Last.Back = theUser.name;
                Last = theUser;
            }
            if (recentUser.Count > recentSize)
            {
                User returnUser;
                string first = First.Back;
                recentUser.TryRemove(First.name, out returnUser);
                First = recentUser[first];
            }
            return theUser;
        }

        /// <summary>
        /// historicalFollows add logic
        /// </summary>
        /// <param name="theUser">Referenced user object</param>
        /// <returns>Updated(?) referenced user object</returns>
        public User addFollowUser(User theUser)
        {
            User tempUser;
            if (historicalFollows.TryGetValue(theUser.name, out tempUser))
            {
                User.lazyUpdate(tempUser, theUser, out theUser);
                historicalFollows.TryUpdate(theUser.name, theUser, historicalFollows[theUser.name]);
                return theUser;
            }
            else
            {
                historicalFollows.TryAdd(theUser.name, theUser);
            }
            return theUser;
        } 

        /// <summary>
        /// Logic for setting the channel's current OW hero. Note that it takes into account
        /// the death timer to ensure that ClipMine data correlates to what hero is actually
        /// being played.
        /// </summary>
        /// <param name="theHero">Referenced hero object</param>
        /// <param name="heroes">Array of both the input and output(updated) hero</param>
        /// <returns>True if added without updating.</returns>
        public bool setCurrentHero(Hero theHero, out Hero[] heroes) //{old hero, new hero}
        {
            TimeSpan ts;
            if (cacheHero == null)
            {
                if (theHero == null)
                {
                    if (cacheWatch == null)
                    {
                        if (currentHero != "")
                        {
                            cacheWatch = new Stopwatch();
                            cacheWatch.Start();
                        }
                    }
                    else
                    {
                        ts = cacheWatch.Elapsed;
                        if (ts.TotalSeconds > timetoCheck && currentHero != "")
                        {
                            Debug.WriteLine(ts.TotalSeconds + " seconds.");
                            heroes = updateCurrentHero(null);
                            return true;
                        }
                    }
                }
                else
                {
                    cacheWatch = new Stopwatch();
                    cacheWatch.Start();
                    cacheHero = theHero;
                }
            }
            else
            {
                if (theHero != null && cacheHero.name == theHero.name)
                {
                    ts = cacheHero.playedTime.Elapsed;
                    Debug.WriteLine(ts.TotalSeconds + " seconds.");
                    if (ts.TotalSeconds > timetoCheck && cacheHero.name != currentHero)
                    {
                        heroes = updateCurrentHero(cacheHero);
                        return true;
                    }
                }
                else if (cacheHero != theHero)
                {
                    cacheHero = theHero;
                    if (theHero == null)
                    {
                        if (currentHero == "") cacheWatch = null;
                        else cacheWatch.Restart();
                    }
                }
            }
            heroes = new Hero[] { null, null };
            return false;
        }

        /// <summary>
        /// Update logic for OW hero. Tracks played time statistic for the particular character.
        /// This list does not have hardset characters as OW comes out with characters overtime.
        /// </summary>
        /// <param name="theHero"></param>
        /// <returns></returns>
        private Hero[] updateCurrentHero(Hero theHero)
        {
            Hero oldHero = null;
            Hero newHero = null;
            if (currentHero != "")
            {
                Hero tempHero;
                if (heroList.TryGetValue(currentHero, out tempHero))
                {
                    tempHero.deactivate();
                    heroList.TryUpdate(tempHero.name, tempHero, heroList[tempHero.name]);
                    oldHero = tempHero;
                }
            }
            if (theHero == null)
            {
                if (currentHero != "") lastHero = currentHero;
                currentHero = "";
            }
            else
            {
                Hero temp2Hero;
                if (heroList.TryGetValue(theHero.name, out temp2Hero))
                {
                    temp2Hero.activate();
                    theHero = temp2Hero;
                }
                else
                {
                    heroList.TryAdd(theHero.name, theHero);
                }
                currentHero = theHero.name;
                newHero = theHero;
            }
            return new Hero[] { oldHero, newHero };
        }

        /// <summary>
        /// Starts a recursive call to FollowTaskHelper to identify new followers from
        /// Twitch Helix
        /// </summary>
        /// <param name="helixdata">Helix API data to sort through</param>
        /// <returns>List of Twitch user id that are new to channel</returns>
        public List<int> GetFollowers(HelixData helixdata)
        {
            int checkSize = followsCache.checkSize >= helixdata.data.Count() ? followsCache.checkSize : helixdata.data.Count();
            //Debug.WriteLine($"helixdata count is {helixdata.data.Count()} and CheckSize of {checkSize}");
            List<int> newFollowers = FollowTaskHelper(followsCache, helixdata, 0, checkSize, new List<int>());
            if (!followsCache.setupComplete) followsCache.setupComplete = true;
            else
            {
                return newFollowers;
            }
            return null;
        }

        /// <summary>
        /// GetFollowers method helper
        /// </summary>
        /// <param name="theCache">Cache to help follower tracking</param>
        /// <param name="helixdata">Helix API data</param>
        /// <param name="i">index of the helixdata during recursion</param>
        /// <param name="size">size of cache</param>
        /// <param name="followerCollection">List of Twitch user id</param>
        /// <returns>List of Twitch user id that are new</returns>
        private List<int> FollowTaskHelper(FollowerCache theCache, HelixData helixdata, int i, int size, List<int> followerCollection)
        {
            if (i < size && !theCache.containsUser(helixdata.data[i].from_id))
            {
                FollowTaskHelper(theCache, helixdata, i + 1, size, followerCollection);
                theCache.addFollower(helixdata.data[i].from_id);
                followerCollection.Add(helixdata.data[i].from_id);
            }
            return followerCollection;
        }

        /// <summary>
        /// Specific call to update user if they are still in chat. If not, throw update to the
        /// recentUsers list.
        /// </summary>
        /// <param name="data">List of user objects to add from</param>
        public void confirmActiveAddRecents(List<User> data) 
        {
            foreach (User item in data)
            {
                User tempUser;
                if (chatUsers.TryGetValue(item.name, out tempUser))
                {
                    User.lazyUpdate(tempUser, item, out tempUser);
                    chatUsers.TryUpdate(tempUser.name, tempUser, chatUsers[tempUser.name]);
                }
                else
                {
                    addRecentUser(item);
                }
            }
        }

        /// <summary>
        /// Takes Twitch Helix API data and attempts to update chatUsers if the user exists.
        /// </summary>
        /// <param name="theData">Twitch Helix API data to pull from</param>
        public void UpdateChatUser(HelixData theData)
        {
            foreach (HelixData.GenericHelixObject item in theData.data)
            {
                User tempUser;
                if (chatUsers.TryGetValue(item.login, out tempUser))
                {
                    User convertedUser = item.convertToUser();
                    User.lazyUpdate(tempUser, convertedUser, out convertedUser);
                    chatUsers.TryUpdate(convertedUser.name, convertedUser, chatUsers[convertedUser.name]);
                }
            }
        }

        /// <summary>
        /// Prepare sql notation for all users that need to be loaded from database.
        /// </summary>
        /// <param name="timerObject">Required but not utilized</param>
        public void prepareLoad(Object timerObject)
        {
            string buffString = "";
            foreach (User user in chatUsers.Values)
            {
                if (!user.checkedDB)
                {
                    if (buffString != "")
                        buffString += $" OR name = '{user.name}'";
                    else
                        buffString += $"name = '{user.name}'";
                    user.checkedDB = true;
                }
            }
            Debug.WriteLine("Attempting prepareLoad on " + buffString);
            if (buffString != "")
                onDBActionArgs?.Invoke(this, new OnDBActionArgs() { channel = channel,
                    listofUsersinSql = buffString, dbActionType = DBActionType.LoadCallHelper });
        }

        /// <summary>
        /// Prepare sql notation for all useres that need to be saved to database.
        /// </summary>
        /// <returns>Returns the sql string for database saving</returns>
        public SqlCommand[] prepareSave()
        {
            SqlCommand saveCommand = new SqlCommand(), delCommand = new SqlCommand(), save2Command = new SqlCommand();
            //saveCommand.CommandText = "Insert Users (name, mod, id, points, modifier, alias, lastCheckIn) Values ";
            //delCommand.CommandText = "Delete from Users Where ";
            //save2Command.CommandText = saveCommand.CommandText;

            bool saved1 = false, saved2 = false;            
            int i = 0;

            ICollection<User> users = historicalFollows.Values, chatUser = chatUsers.Values;
            List<User> userList = new List<User>(users.Count() + chatUser.Count);
            userList.AddRange(users);
            userList.AddRange(chatUser);

            StringBuilder saveBuilder = new StringBuilder(), delBuilder = new StringBuilder(), save2Builder = new StringBuilder();
            saveBuilder.Append("Insert Users (name, mod, id, points, modifier, alias, lastCheckIn, regular) Values ");
            delBuilder.Append("Delete from Users Where ");
            save2Builder.Append(saveBuilder.ToString());

            foreach (User theUser in userList)
            {
                if (theUser.lastCheckIn == null)
                {
                    theUser.lastCheckIn = DateTime.Now;
                }
                string valueString = $"@name{i}, @mod{i}, @id{i}, @points{i}, @modifier{i}, @alias{i}, @last{i}";
                if (!theUser.existsDB)
                {
                    saveBuilder.Append("(" + valueString + "),");
                    saveCommand.Parameters.AddWithValue($"@name{i}", theUser.name);
                    saveCommand.Parameters.AddWithValue($"@mod{i}", theUser.mod);
                    saveCommand.Parameters.AddWithValue($"@id{i}", theUser.userId);
                    saveCommand.Parameters.AddWithValue($"@points{i}", theUser.points);
                    saveCommand.Parameters.AddWithValue($"@modifier{i}", theUser.modifier);
                    saveCommand.Parameters.AddWithValue($"@alias{i}", theUser.alias);
                    saveCommand.Parameters.AddWithValue($"@last{i}", theUser.lastCheckIn);
                    saveCommand.Parameters.AddWithValue($"@regular{i}", theUser.regular);
                    if (!saved1) saved1 = true;

                } else
                {
                    delBuilder.Append($"name = @name{i} OR ");
                    delCommand.Parameters.AddWithValue($"@name{i}", theUser.name);

                    save2Builder.Append("(" + valueString + "),");
                    save2Command.Parameters.AddWithValue($"@name{i}", theUser.name);
                    save2Command.Parameters.AddWithValue($"@mod{i}", theUser.mod);
                    save2Command.Parameters.AddWithValue($"@id{i}", theUser.userId);
                    save2Command.Parameters.AddWithValue($"@points{i}", theUser.points);
                    save2Command.Parameters.AddWithValue($"@modifier{i}", theUser.modifier);
                    save2Command.Parameters.AddWithValue($"@alias{i}", theUser.alias);
                    save2Command.Parameters.AddWithValue($"@last{i}", theUser.lastCheckIn);
                    save2Command.Parameters.AddWithValue($"@regular{i}", theUser.regular);
                    if (!saved2) saved2 = true;
                }
                i++;
            }
            SqlCommand saved1Command = null, deletedCommand = null, saved2Command = null;
            
            if (saved1)
            {
                int chopper = saveBuilder.Length;
                saveCommand.CommandText = saveBuilder.ToString().Substring(0, chopper - 1);
                saved1Command = saveCommand;
            }
            if (saved2)
            {
                int chopper = save2Builder.Length;
                save2Command.CommandText = save2Builder.ToString().Substring(0, chopper - 1);
                saved2Command = save2Command;
                chopper = delBuilder.Length;
                delCommand.CommandText = delBuilder.ToString().Substring(0, chopper - 4);
                deletedCommand = delCommand;
            }
            return new SqlCommand[3] { saved1Command, deletedCommand, save2Command};
        }

        /// <summary>
        /// Add points to users in chatUsers
        /// </summary>
        /// <param name="pointObject">Required but not utilized</param>
        public void pointAdder(object pointObject) {
            List<string> users = chatUsers.Keys.ToList();
            foreach (string user in users)
            {
                chatUsers[user].points += pointValue;
            }
        }

        /// <summary>
        /// Follower helper object to assist for checking for new follows
        /// </summary>
        public class FollowerCache
        {

            Queue<int> followerQ;
            List<int> followerTable;
            public int checkSize { get; set; }
            public string channel { get; set; }
            public int channel_id { get; set; }
            public bool setupComplete { get; set; }

            public FollowerCache()
            {
                followerQ = new Queue<int>();
                setupComplete = false;
                followerTable = new List<int>();
            }

            public void addFollower(int twitchUser)
            {
                if (followerQ.Count >= checkSize)
                {
                    int id = followerQ.Dequeue();
                    followerTable.Remove(id);
                }

                followerQ.Enqueue(twitchUser);
                followerTable.Add(twitchUser);
            }

            public bool containsUser(int key)
            {
                return followerTable.Contains(key);
            }
        }
    }
}
