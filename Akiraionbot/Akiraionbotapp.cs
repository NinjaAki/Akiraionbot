using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Json;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Speech.Synthesis;
using Akiraionbot.Models.TwitchAPI.Helix;
using Akiraionbot.Models;
using Akiraionbot.IRC;
using Akiraionbot.https;
using Akiraionbot.Events;
using Akiraionbot.Enums;
using System.Data.SqlClient;

namespace AkiraionbotForm
{
    public partial class Akiraionbotapp : Form
    {
        #region credentials
        private static string password = "";
        private static string client_id = "";
        #endregion
        #region variables
        private static string username = "";
        private static string owner = "";
        IRCClient irc;
        TwitchHelix twitchHelix;
        string chatTab = "chatDisplay";
        string listViewSuffix = "chat";
        NetworkStream serverStream = default(NetworkStream);
        Dictionary<string, RichTextBox> channelsJoined = new Dictionary<string, RichTextBox>();
        Dictionary<string, ChannelBackBone> channelArray = new Dictionary<string, ChannelBackBone>();
        Dictionary<String, int> botChannels = new Dictionary<string, int>();
        List<System.Threading.Timer> timers = new List<System.Threading.Timer>();
        ConcurrentQueue<MessageInfo> MessagestoPost = new ConcurrentQueue<MessageInfo>();
        ConcurrentQueue<MessageInfo> UsersToUpdate = new ConcurrentQueue<MessageInfo>();
        bool consoleOpen;
        int chatHistory = 200;
        Thread getRawMessages;
        Thread parseRawMessages;
        Thread checkMessages;
        Thread sendMessages;
        Thread postMessages;
        Thread apiCalling;
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        #endregion

        /// <summary>
        /// Initiates; attempts creation of tcp connection using credentials specified in
        /// credentials file.
        /// </summary>
        public Akiraionbotapp()
        {
            if (client_id == "")
            {
                StreamReader file = new StreamReader(@"streamfolder\credentials.txt");
                string userClientPattern = @".*;.*";
                string passPassPattern = @"password=\s?.*";
                string ownPattern = @"owner=\s?.*";
                string clientIDPattern = @"(?<=(;)).*";
                string usernamePattern = @"(?=(\w+;))\w+";
                string passwordPattern = @"(?<=(password=\s?)).*";
                string ownerPattern = @"(?<=(owner=\s?)).*";
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (Regex.IsMatch(line, userClientPattern))
                    {
                        username = Regex.Match(line, usernamePattern).ToString();
                        client_id = Regex.Match(line, clientIDPattern).ToString();
                    }
                    else if (Regex.IsMatch(line, passPassPattern))
                    {
                        password = Regex.Match(line, passwordPattern).ToString();
                    }
                    else if (Regex.IsMatch(line, ownPattern))
                    {
                        owner = Regex.Match(line, ownerPattern).ToString();
                    }
                }
            }
            irc = new IRCClient("irc.twitch.tv", 6667, username, password);
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender">Required but not utilized</param>
        /// <param name="e">Required but not utilized</param>
        private void Akiraionbotapp_Load(object sender, EventArgs e)
        {
            consoleOpen = true;
            twitchHelix = new TwitchHelix(client_id, "akiraion");

            //client id is hardcoded.
            if (owner != "")
            {
                botChannels.Add(owner, 28066706);
                joinHandler(owner);
            }
            //This is your monitor channel
            joinHandler(username);

            twitchHelix.onHelixCallArgs += onHelixCall;
            irc.onNoFileOpen += onFileOpen;

            if (owner != "")
            {
                Task.Factory.StartNew(() => irc.loadlocalCommands(owner, irc.commandsPath)); //make sure to add channel to channelArray first
            }

            channelsJoined.Add(chatTab, chatDisplay);

            getRawMessages = new Thread(messageRetriever);
            getRawMessages.Name = "IRC Message Retriever Thread";
            getRawMessages.Start();
            parseRawMessages = new Thread(messageParser);
            parseRawMessages.Name = "Message Parsing Thread";
            parseRawMessages.Start();
            checkMessages = new Thread(messageChecker);
            checkMessages.Name = "Message Checker Thread";
            checkMessages.Start();
            sendMessages = new Thread(messageSender);
            sendMessages.Name = "Send to IRC Thread";
            sendMessages.Start();
            postMessages = new Thread(messagePoster);
            postMessages.Name = "Post to Console Thread";
            postMessages.Start();
            apiCalling = new Thread(apiCaller);
            apiCalling.Name = "Twitch API Calling Thread";
            apiCalling.Start();
        }

        /// <summary>
        /// Attempt to dispose of resources upon close.
        /// </summary>
        /// <param name="sender">Required but not utilized</param>
        /// <param name="e">Required but not utilized</param>
        private void Akiraionbotapp_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveDB();
            consoleOpen = false;
            //Update this to take in account multiple channels joined.
            irc.leaveRoom();
            foreach (System.Threading.Timer element in timers) {
                element.Dispose();
            }
            serverStream.Dispose();
            Environment.Exit(0);
        }

        /// <summary>
        /// Join logic for a specific Twitch channel. This will handled UI differentiation
        /// for channels you want to join with bot and channels you want to moderate on.
        /// </summary>
        /// <param name="channel">Referenced channel</param>
        private void joinHandler(string channel)
        {
            irc.joinRoom(channel);
            channelArray.Add(channel, new ChannelBackBone(channel));

            TabPage newTab = new TabPage();
            RichTextBox textDisplay = new RichTextBox();
            ListView userView = new ListView();

            textDisplay.TextChanged += (s, e) =>
            {
                textDisplay.SelectionStart = textDisplay.Text.Length;
                textDisplay.ScrollToCaret();
            };                
            textDisplay.Name = channel;
            textDisplay.Location = new Point(3, 3);
            textDisplay.Size = new Size(570, 450);
            textDisplay.BackColor = Color.Black;
            textDisplay.ForeColor = Color.White;
            textDisplay.LanguageOption = RichTextBoxLanguageOptions.UIFonts;

            userView.Name = channel + listViewSuffix;
            userView.Location = new Point(575, 3);
            userView.UseCompatibleStateImageBehavior = false;
            userView.Columns.Add("Chatters", -2, HorizontalAlignment.Left);
            userView.View = View.Details;
            userView.MouseClick += (s, e) =>
            {
                clearListItemColor(userView);
            };
            userView.Sorting = System.Windows.Forms.SortOrder.Ascending;

            int successValue;
            if (botChannels.TryGetValue(channel, out successValue))
            {

                userView.Size = new Size(155, 300);
                //grabbed from https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/threading/thread-timers

                ChannelBackBone.FollowerCache followerCache = new ChannelBackBone.FollowerCache{ checkSize = 50, channel = channel, channel_id = successValue };
                channelArray[channel].followsCache = followerCache;

                TimerCallback timerCallback = new TimerCallback(FollowerTask);
                System.Threading.Timer followerTimer =
                    new System.Threading.Timer(timerCallback, followerCache, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
                timers.Add(followerTimer);


                ChannelBackBone.FollowerCache follower2Cache = new ChannelBackBone.FollowerCache { channel = channel, channel_id = successValue };

                TimerCallback timer2Callback = new TimerCallback(CheckFollowerTask);
                System.Threading.Timer follower2Timer =
                    new System.Threading.Timer(timer2Callback, follower2Cache, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
                timers.Add(follower2Timer);

                TimerCallback timer3Callback = new TimerCallback(channelArray[channel].prepareLoad);
                System.Threading.Timer dbTimer =
                    new System.Threading.Timer(timer3Callback, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3));
                timers.Add(dbTimer);

                TimerCallback timer4Callback = new TimerCallback(channelArray[channel].pointAdder);
                System.Threading.Timer pointTimer =
                    new System.Threading.Timer(timer4Callback, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
                timers.Add(pointTimer);

                irc.onDBActionArgs += onDBAction;
                channelArray[channel].onDBActionArgs += onDBAction;                

                ListView notificationView = new ListView();
                notificationView.Name = channel;
                notificationView.Size = new Size(155, 145);
                notificationView.Location = new Point(575, 305);
                notificationView.UseCompatibleStateImageBehavior = false;
                notificationView.Columns.Add("Notifications", -2, HorizontalAlignment.Left);
                notificationView.View = View.Details;
                notificationView.MouseDoubleClick += (s, e) =>
                {
                    notificationView.Clear();
                };

                channelArray[channel].notificationLV = notificationView;

                newTab.Controls.Add(notificationView);

                ListViewGroup viewerGroup = new ListViewGroup("Followers", HorizontalAlignment.Left);
                viewerGroup.Name = "Followers";
                notificationView.Groups.Add(viewerGroup);
            }
            else
            {
                userView.Size = new Size(155, 450);
            }

            newTab.Controls.Add(textDisplay);
            newTab.Controls.Add(userView);
            newTab.Text = "#" + channel;

            chatTabControl.TabPages.Add(newTab);

            channelArray[channel].chatLV = userView;
            channelsJoined.Add(textDisplay.Name, textDisplay);
        }

        /// <summary>
        /// Timer Task that will initiate a follower tracker logic using Twitch Helix API. This will
        /// check for new users.
        /// </summary>
        /// <param name="followCache">Will hold recent followers to compare against</param>
        private async void FollowerTask(object followCache)
        {
            ChannelBackBone.FollowerCache followerCache = (ChannelBackBone.FollowerCache)followCache;
            string getFollow = $"users/follows?to_id={followerCache.channel_id}&first={followerCache.checkSize}";
            await twitchHelix.helixCall(getFollow, followerCache.channel, ApiCallType.FollowerCall);
        }

        /// <summary>
        /// Timer Task that will check users that are currently in the channel to see if they
        /// are follower.
        /// </summary>
        /// <param name="theCache">Helper object</param>
        private void CheckFollowerTask(object theCache)
        {
            ChannelBackBone.FollowerCache followerCache = (ChannelBackBone.FollowerCache)theCache;
            string tempString = $"?to_id={followerCache.channel_id}";
            foreach (User user in channelArray[followerCache.channel].chatUsers.Values)
            {
                if (user.followed_at == null && user.userId != 0)
                {
                    string url = $"{tempString}&from_id={user.userId}";
                    twitchHelix.ApiQueue.Enqueue(new TwitchHelix.HelixHelper(url, followerCache.channel, ApiCallType.DoesFollowerCall));
                }
            }
        }

        /// <summary>
        /// Default Helix Meta call created when a channel is added as a botchannel.
        /// </summary>
        /// <param name="channel">Twitch channel to setup call for</param>
        private void OverwatchTask(object channel)
        {
            string theChannel = (string)channel;
            string url = @"streams/metadata?user_login=" + theChannel;
            twitchHelix.ApiQueue.Enqueue(new TwitchHelix.HelixHelper(url, theChannel, ApiCallType.OverwatchHeroCall));
        }

        /// <summary>
        /// This thread checks tcp port for incoming IRC messages every millisecond.
        /// </summary>
        private void messageRetriever()
        {
            serverStream = irc.tcpClient.GetStream();
            int buffSize = 0;
            byte[] inStream = new byte[10025];
            buffSize = irc.tcpClient.ReceiveBufferSize;
            while(consoleOpen)
            {
                try
                {
                    irc.readMessage();
                    Thread.Sleep(1);
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e.StackTrace);
                }
            }
        }

        /// <summary>
        /// This thread will check for messages that instantiates objects or methods that need to
        /// be saved to variables under Akiriaonbotapp.
        /// </summary>
        private void messageChecker()
        {
            while(consoleOpen)
            {
                analyzeMessage();
                Thread.Sleep(50);                
            }
        }

        /// <summary>
        /// Check PRIVMSG for specific commands.
        /// </summary>
        private void analyzeMessage()
        {
            MessageInfo privMsg;
            irc.botLogic(out privMsg); //returns true if botlogic did something to the message
            if (privMsg != null)
            {
                MessagestoPost.Enqueue(privMsg);
                
                if (privMsg.theUser.name == "akiraion")
                {
                    Match theMatch = Regex.Match(privMsg.message, $@"\A!track \w+");
                    if (theMatch.Success)
                    {
                        string theChannel = theMatch.ToString().Substring(7);
                        if (!channelArray.ContainsKey(theChannel)) channelArray.Add(theChannel, new ChannelBackBone(theChannel));
                        TimerCallback TimerDelegate = new TimerCallback(OverwatchTask);
                        System.Threading.Timer TimerItem =
                            new System.Threading.Timer(TimerDelegate, theChannel, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
                        timers.Add(TimerItem);
                    }
                }
                Match theMatch2 = Regex.Match(privMsg.message, $@"\A!points\Z");
                if (theMatch2.Success)
                {
                    if (channelArray[privMsg.channel].containsUser(privMsg.theUser.name))
                    {
                        int thePoints = channelArray[privMsg.channel].returnUser(privMsg.theUser.name).points;
                        irc.sendQ.Enqueue(new string[2] { privMsg.channel, $"@{privMsg.theUser.name} has {thePoints} points." });
                    }
                }

            }
        }

        /// <summary>
        /// This thread will check for messages to post to the UI.
        /// </summary>
        private void messagePoster()
        {
            synthesizer.Volume = 10;  // 0...100
            synthesizer.Rate = 0;     // -10...10

            while (consoleOpen)
            {
                checkPost();
                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// Logic for posting to UI.
        /// </summary>
        private void checkPost()
        {
            if (this.InvokeRequired) this.Invoke(new MethodInvoker(checkPost));
            else
            {
                if (MessagestoPost != null && !MessagestoPost.IsEmpty)
                {
                    MessageInfo grabbedMsg = null;
                    bool messageRetrieved = MessagestoPost.TryDequeue(out grabbedMsg);
                    if (messageRetrieved)
                    {
                        RichTextBox referredRTB = channelsJoined[grabbedMsg.channel];
                        referredRTB.Text += grabbedMsg.theUser.name + " : " + grabbedMsg.message + Environment.NewLine;

                        if (botChannels.ContainsKey(grabbedMsg.channel) && grabbedMsg.ttsMessage != "")
                        {
                            if (channelArray[grabbedMsg.channel].lastPerson == grabbedMsg.theUser.name)
                                synthesizer.SpeakAsync(grabbedMsg.ttsMessage);
                            else
                            {
                                synthesizer.SpeakAsync(grabbedMsg.theUser.name + " : " + grabbedMsg.ttsMessage);
                                channelArray[grabbedMsg.channel].lastPerson = grabbedMsg.theUser.name;
                            }
                        }

                        if (referredRTB.Lines.Length > chatHistory)
                        {
                            referredRTB.Select(0, referredRTB.GetFirstCharIndexFromLine(1));
                            referredRTB.SelectedText = "";
                        }
                    }
                    
                }
            }
        }

        /// <summary>
        /// Runs on the UI thread to update the specific part of the UI depending on the type of
        /// message that is retrieved.
        /// </summary>
        private void updateUserView()
        {
            if (this.InvokeRequired) this.Invoke(new MethodInvoker(updateUserView));
            else
            {
                ListView listView = null;
                if (UsersToUpdate.Count > 0)
                {
                    MessageInfo messageInfo = null;
                    if (UsersToUpdate.TryDequeue(out messageInfo))
                    {
                        User tempUser;
                        try
                        {
                            switch (messageInfo.header)
                            {
                                case IRCMessageType.JOIN:
                                    bool joinBool = channelArray[messageInfo.channel].addUser(messageInfo.theUser, out tempUser);
                                    if (!joinBool && tempUser != null)
                                    {
                                        listView = channelArray[messageInfo.channel].chatLV;
                                        ListViewItem queryUser = null;
                                        queryUser = listView.FindItemWithText(tempUser.name, false, 0, false);
                                        if (queryUser == null) queryUser = listView.FindItemWithText(tempUser.name, false, 1, false);
                                        Debug.WriteLine("----------------------" + tempUser.ToString().Substring(0, 1) == User.modSym);
                                        if (queryUser != null)
                                        {
                                            Debug.WriteLine($"66666666666666666 Found the {queryUser.Text} item");
                                            //if (queryUser.Text == ircUser.name || queryUser.Text == IRCUser.modSym + ircUser.name)
                                            //{
                                            listView.Items.Remove(queryUser);
                                            queryUser.Text = tempUser.ToString();
                                            listView.Items.Add(queryUser);
                                            //}
                                        }
                                    }
                                    else if (joinBool)
                                    {
                                        listView = channelArray[messageInfo.channel].chatLV;
                                        ListViewItem listItem = new ListViewItem(messageInfo.theUser.ToString());
                                        listItem.BackColor = Color.Red;
                                        listItem.Name = messageInfo.theUser.name;
                                        listView.Items.Add(listItem);
                                    }
                                    break;
                                case IRCMessageType.PART:
                                    User queryIRCUser = channelArray[messageInfo.channel].removeActiveUser(messageInfo.theUser.name);
                                    if (queryIRCUser != null)
                                    {
                                        listView = channelArray[messageInfo.channel].chatLV;
                                        ListViewItem queryUser = listView.FindItemWithText(messageInfo.theUser.name, false, 0, false);
                                        if (queryUser == null)
                                        {
                                            queryUser = listView.FindItemWithText(messageInfo.theUser.name, false, 1, false);
                                        }
                                        if (queryUser != null)
                                        {
                                            listView.Items.Remove(queryUser);
                                            //Task removeUserTask = Task.Factory.StartNew(() => channelArray[messageInfo.channel].removeActiveUser(messageInfo.theUser));
                                        }
                                    }
                                    break;
                                case (IRCMessageType.OP):
                                    bool opBool = channelArray[messageInfo.channel].addUser(messageInfo.theUser, out tempUser);
                                    if (!opBool && tempUser != null)
                                    {
                                        listView = channelArray[messageInfo.channel].chatLV;
                                        ListViewItem queryUser = listView.FindItemWithText(tempUser.name, false, 0, false);
                                        if (queryUser == null)
                                        {
                                            queryUser = listView.FindItemWithText(tempUser.ToString(), false, 0, false);
                                        }
                                        if (queryUser != null)
                                        {
                                            listView.Items.Remove(queryUser);
                                            ListViewItem updatedUser = new ListViewItem(tempUser.ToString());
                                            listView.Items.Add(updatedUser);
                                        }
                                    }
                                    else
                                    {
                                        listView = channelArray[messageInfo.channel].chatLV;
                                        ListViewItem updatedUser = new ListViewItem(messageInfo.theUser.ToString());
                                        updatedUser.BackColor = Color.Red;
                                        listView.Items.Add(updatedUser);
                                    }
                                    break;
                                case (IRCMessageType.DEOP):
                                    if (messageInfo.theUser.name != messageInfo.channel)
                                    {
                                        if (channelArray[messageInfo.channel].containsUser(messageInfo.theUser.name))
                                        {
                                            User recordUser = channelArray[messageInfo.channel].returnUser(messageInfo.theUser.name);
                                            User.forceUpdate(recordUser, messageInfo.theUser, out recordUser);
                                            listView = channelArray[messageInfo.channel].chatLV;
                                            ListViewItem queryUser = listView.FindItemWithText(messageInfo.theUser.name, false, 0, false);
                                            if (queryUser == null)
                                            {
                                                queryUser = listView.FindItemWithText(messageInfo.theUser.ToString(), false, 0, false);
                                            }
                                            if (queryUser != null)
                                            {
                                                listView.Items.Remove(queryUser);
                                                ListViewItem updatedUser = new ListViewItem(messageInfo.theUser.ToString());
                                                listView.Items.Add(updatedUser);
                                            }
                                        }
                                    }
                                    break;
                                case (IRCMessageType.FOLLOWER):
                                    listView = channelArray[messageInfo.channel].notificationLV;
                                    ListViewItem theItem = new ListViewItem(messageInfo.theUser.name, listView.Groups["Followers"]);
                                    theItem.BackColor = Color.Red;
                                    theItem.Name = messageInfo.theUser.name;
                                    listView.Items.Add(theItem);
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.StackTrace);
                            Debug.WriteLine("See updateUserView in Akiraionbotapp.cs");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This thread will attempt to retrieve messages from a channel queue to route it to the
        /// correct place on the UI where it should be posted.
        /// </summary>
        private void messageParser()
        {
            while(consoleOpen)
            {
                MessageInfo retrievedMessage = irc.parseMessage();

                if (retrievedMessage != null)
                {
                    if (retrievedMessage.header == IRCMessageType.OTHER)
                    {
                        retrievedMessage.channel = chatTab;
                        MessagestoPost.Enqueue(retrievedMessage);
                    }
                    else if (retrievedMessage.header == IRCMessageType.PRIVMSG)
                    {
                        if (botChannels.ContainsKey(retrievedMessage.channel))
                        {
                            User recordUser = channelArray[retrievedMessage.channel].returnUser(retrievedMessage.theUser.name);
                            if (recordUser != null)
                            {
                                User.lazyUpdate(recordUser, retrievedMessage.theUser, out recordUser);
                                retrievedMessage.theUser = recordUser;
                            }
                            irc.chatMessageQ.Enqueue(retrievedMessage);
                        }
                        else MessagestoPost.Enqueue(retrievedMessage);
                        MessageInfo copyMI = new MessageInfo(retrievedMessage.theUser.name, retrievedMessage.channel, "");
                        copyMI.header = IRCMessageType.JOIN;
                        UsersToUpdate.Enqueue(copyMI);
                        updateUserView();
                    }
                    else if (retrievedMessage.header == IRCMessageType.PART
                        || retrievedMessage.header == IRCMessageType.JOIN)
                    {
                        UsersToUpdate.Enqueue(retrievedMessage);
                        updateUserView();
                    }
                    else if (retrievedMessage.header == IRCMessageType.OP
                        || retrievedMessage.header == IRCMessageType.DEOP)
                    {
                        UsersToUpdate.Enqueue(retrievedMessage);
                        updateUserView();
                    }
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// This thred will send messages to the Twitch IRC abiding to the default message limit of
        /// 20 message every 30 seconds.
        /// </summary>
        private void messageSender()
        {
            while (consoleOpen)
            {
                irc.sendMessage();
                Thread.Sleep(20/30*1000);
            }
        }
        
        /// <summary>
        /// This thread will run the api call to Twitch Helix API abiding to the default rate limit
        /// of 30 calls per minute.
        /// </summary>
        private void apiCaller()
        {
            while (consoleOpen)
            {
                twitchHelix.runApiCall();
                Thread.Sleep(60/30*1000);
            }
        }

        /// <summary>
        /// This eventhandler captures the Twitch Helix call results and routes it appropriately
        /// to the next required step.
        /// </summary>
        /// <param name="sender">Required but not utilized; this will be the TwitchHelix object.</param>
        /// <param name="e">Callback results for next steps.</param>
        private void onHelixCall(Object sender, OnHelixCallArgs e)
        {
            //Debug.WriteLine($"hhhhhhhhhhhh onHelixCall thread is {Thread.CurrentThread.ManagedThreadId} hhhhhhhhhhhh");
            switch (e.type)
            {
                case ApiCallType.FollowerCall:
                    List<int> newFollowers = channelArray[e.channel].GetFollowers(e.response);
                    if (newFollowers != null && newFollowers.Count > 0)
                    {
                        StringBuilder urlString = new StringBuilder("users?");
                        foreach (int item in newFollowers)
                        {
                            urlString.Append("&id=" + item);
                        }
                        Task<HelixData> userCallData = twitchHelix.helixCall(urlString.ToString(), e.channel, ApiCallType.NoCall);
                        HelixData userData = userCallData.Result;
                        
                        List<User> newFollowerIRCUsers = new List<User>();
                        foreach (HelixData.GenericHelixObject item in userData.data)
                        {
                            UsersToUpdate.Enqueue(new MessageInfo(item.login, e.channel, "") { header = IRCMessageType.FOLLOWER });
                            newFollowerIRCUsers.Add(new User(item.login) {
                                followed_at = item.followed_at, userId = item.id, displayName = item.display_name
                            });
                        }
                        channelArray[e.channel].confirmActiveAddRecents(newFollowerIRCUsers);
                    }
                    break;
                case ApiCallType.OverwatchHeroCall:
                    if (e.response != null && e.response.data.Count() != 0 && e.response.data[0].overwatch != null)
                    {
                        if (e.response.data[0].overwatch.broadcaster != null)
                        {
                            Hero theHero = e.response.data[0].overwatch.broadcaster.hero;
                            if (theHero != null)
                            {
                                Hero[] heroArray;
                                bool heroUpdated = channelArray[e.channel].setCurrentHero(theHero, out heroArray);
                                if (heroUpdated && !(heroArray[0] == null && heroArray != null
                                    && channelArray[e.channel].lastHero == heroArray[1].name))
                                {
                                    string messageToSend = "";
                                    if (heroArray[0] != null)
                                    {
                                        messageToSend += $" played {heroArray[0].name} before " +
                                            $"with {heroArray[0].returnDuration()} played time";
                                    }
                                    if (heroArray[1] != null)
                                    {
                                        if (messageToSend != "") messageToSend += " and";
                                        messageToSend += $" is now playing the hero {heroArray[1].name}.";
                                    }
                                    if (messageToSend != "")
                                    {
                                        messageToSend = e.channel + messageToSend;
                                        string[] enqueueMessage = new string[] { "akiraion", messageToSend };
                                        irc.sendQ.Enqueue(enqueueMessage);
                                    }
                                }
                            }
                            else
                            {
                                Hero[] heroArr;
                                channelArray[e.channel].setCurrentHero(null, out heroArr);
                            }
                        }

                    }
                    break;
                case ApiCallType.DoesFollowerCall:
                    if (e.response.data.Count() > 0)
                    {
                        StringBuilder urlString = new StringBuilder("users?");
                        foreach (HelixData.GenericHelixObject item in e.response.data)
                        {
                            urlString.Append("&id=" + item.from_id);
                        }
                        Task<HelixData> userCallData = twitchHelix.helixCall(urlString.ToString(), e.channel, ApiCallType.NoCall);
                        HelixData userData = userCallData.Result;
                        channelArray[e.channel].UpdateChatUser(userData);
                    }
                    break;
            }
            updateUserView();
        }

        /// <summary>
        /// Captures all file IO Exceptions and will appropriately invoke on the UI thread to attempt
        /// opening of a file from local drive.
        /// </summary>
        /// <param name="sender">Required but not utilized.</param>
        /// <param name="e">Used to figure out what type of error occured and the next steps.</param>
        private void onFileOpen(Object sender, OnNoFileOpenArgs e)
        {
            Debug.WriteLine("onFileOpen called on ThreadID: " + Thread.CurrentThread.ManagedThreadId);
            if (this.InvokeRequired) this.Invoke((MethodInvoker)delegate {
                onFileOpen(sender, e);
            });
            else
            {
                OpenFileDialog openDialog = new OpenFileDialog();
                openDialog.Filter = "mdf files (*.mdf)|*.mdf|txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openDialog.RestoreDirectory = true;
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    string fileName = openDialog.FileName;
                    switch (e.fileError)
                    {
                        case FileErrorType.NoCommandsFile:
                            if (Regex.IsMatch(fileName, @".*(\.txt)\Z"))
                                Task.Factory.StartNew(() => irc.loadlocalCommands(e.channel, fileName));
                            else Debug.WriteLine("File type was incorrect.");
                            break;
                        case FileErrorType.NoDatabaseFile:
                            if (Regex.IsMatch(fileName, @".*(\.mdf)\Z"))
                            {
                                SqlConnection conn = new SqlConnection(@"Data source=.\SQLExpress; Integrated Security=true; AttachDbFilename="
                                    + fileName + "; Trusted_Connection=Yes; User Instance=true; Database=dbo.Users; "
                                    + "MultipleActiveResultSets=true; Connect Timeout=30");
                                irc.connDict.TryAdd(e.channel, conn);
                                Task.Factory.StartNew(() => irc.loadDatabase(e.channel, e.listofUsersSql));
                            }
                            else Debug.WriteLine("File type was incorrect.");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Captures all database results and who initially made the call for routing to the next
        /// steps.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onDBAction(Object sender, OnDBActionArgs e)
        {
            switch (e.dbActionType)
            {
                case DBActionType.LoadCall:
                    User ignoreUser;
                    channelArray[e.channel].addUser(e.user, out ignoreUser);
                    break;
                case DBActionType.LoadCallHelper:
                    irc.loadDatabase(e.channel, e.listofUsersinSql);
                    break;
            }
        }

        /// <summary>
        /// Attempt to save content to database file for each active SQLConnection that is stored.
        /// </summary>
        private void SaveDB()
        {
            ICollection<string> tempList = irc.connDict.Keys;
            if (tempList.Count > 0)
            {
                foreach (string con in tempList)
                {
                    SqlCommand[] sqlToExcecute = channelArray[con].prepareSave();
                    SqlConnection theCon = irc.connDict[con];
                    if (theCon.State == ConnectionState.Open)
                    {
                        theCon.Close();
                    }
                    foreach (SqlCommand com in sqlToExcecute)
                    {
                        if (com != null)
                        {
                            com.Connection = theCon;
                            theCon.Open();
                            try
                            {
                                com.ExecuteNonQuery();
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine($"Error with CommandText of - {com.CommandText} - and stacktrace:\n {e.StackTrace}");
                            }
                            theCon.Close();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Default rich text box event method to add upon creation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chatDisplay_TextChanged(object sender, EventArgs e)
        {
            chatDisplay.SelectionStart = chatDisplay.Text.Length;
            chatDisplay.ScrollToCaret();
        }

        /// <summary>
        /// Default ListView method that should be added upon creation.
        /// </summary>
        /// <param name="listView">Referenced ListView</param>
        private void clearListItemColor(ListView listView)
        {
            ListView.ListViewItemCollection theCollection = listView.Items;
            foreach(ListViewItem li in theCollection)
            {
                li.BackColor = Color.White;
            }
        }
    }
}
