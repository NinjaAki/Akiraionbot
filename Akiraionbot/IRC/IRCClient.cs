using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Akiraionbot.Models.TwitchAPI.Helix;
using Akiraionbot.Models;
using Akiraionbot.Enums;
using System.Windows.Forms;
using Akiraionbot.Events;

namespace Akiraionbot.IRC
{
    class IRCClient
    {
        private string username;
        private string channel;
        private string chatHost = "tmi.twitch.tv";

        public TcpClient tcpClient;
        private StreamReader inputStream;
        private StreamWriter outputStream;

        private ConcurrentQueue<string> messageQ;
        public ConcurrentQueue<string[]> sendQ;
        public ConcurrentQueue<MessageInfo> chatMessageQ;

        public ConcurrentDictionary<string, ConcurrentDictionary<string, string>> commandDict;
        public ConcurrentDictionary<string, SqlConnection> connDict;
        public string commandsPath = @"streamfolder\commands.txt";

        private string linkPattern = @"(https?:\/\/)?([\da-z\.-]+)\.(([a-z]\.?){1,3})([\/\w\.-]*)*\/?";
        private string staticCommand = @"\A!\w+\Z";
        private string dynamicCommand = @"\A!(\w+\s?)+";

        private int ttsCountLimit = 50;
        private int ttsCharLimit = 140;

        public event EventHandler<OnNoFileOpenArgs> onNoFileOpen;
        public event EventHandler<OnDBActionArgs> onDBActionArgs;

        /// <summary>
        /// Constructor to setup irc connection.
        /// </summary>
        /// <param name="ip">ip for tcp client</param>
        /// <param name="port">port for tcp client</param>
        /// <param name="username">username to authenticate with</param>
        /// <param name="password">password to authenticate with</param>
        public IRCClient(string ip, int port, string username, string password)
        {
            try
            {
                tcpClient = new TcpClient(ip, port);
                inputStream = new StreamReader(tcpClient.GetStream());
                outputStream = new StreamWriter(tcpClient.GetStream());

                messageQ = new ConcurrentQueue<string>();
                sendQ = new ConcurrentQueue<string[]>();
                chatMessageQ = new ConcurrentQueue<MessageInfo>();

                commandDict = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
                connDict = new ConcurrentDictionary<string, SqlConnection>();

                this.username = username;

                outputStream.WriteLine("PASS " + password);
                outputStream.WriteLine("NICK " + username);
                outputStream.WriteLine("USER " + " 8 * :" + username);

                //Option commands to send to Twitch
                outputStream.WriteLine("CAP REQ :twitch.tv/membership");
                outputStream.WriteLine("CAP REQ :twitch.tv/tags");
                outputStream.WriteLine("CAP REQ : twitch.tv/commands");
                outputStream.Flush();
            } catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace);
            }
        }

        /// <summary>
        /// Retrieve raw messages from Twitch IRC.
        /// </summary>
        public void readMessage()
        {
            string message = "";
            message = inputStream.ReadLine();
            messageQ.Enqueue(message);
        }

        /// <summary>
        /// Messasge sender helper.
        /// </summary>
        /// <param name="channel">Channel to send message to</param>
        /// <param name="message">Message to send</param>
        private void sendChatMessage(string channel, string message)
        {
            sendIRCMessage($@":{username}!{username}@{username}.{chatHost} PRIVMSG #{channel} :{message}");
        }

        /// <summary>
        /// Messege sending method.
        /// </summary>
        /// <param name="message">IRC message to send</param>
        private void sendIRCMessage(string message)
        {
            outputStream.WriteLine(message);
            outputStream.Flush();
        }

        /// <summary>
        /// The ping response.
        /// </summary>
        public void pong()
        {
            sendIRCMessage("PONG :" + chatHost + "\r\n");
        }

        /// <summary>
        /// Will check new Message objects for inital manipulation. 
        /// </summary>
        /// <remarks>
        /// This could be merged with another method as it currently only checks for ping.
        /// </remarks>
        /// <returns>The new Message objects</returns>
        public MessageInfo parseMessage()
        {
            MessageInfo returnMessage = null;
            if (messageQ != null && !messageQ.IsEmpty)
            {
                string retrievedMessage = "";
                bool messageRetrieved = messageQ.TryDequeue(out retrievedMessage);
                if (messageRetrieved)
                {
                    if (retrievedMessage.Equals("PING :" + chatHost)) pong();
                    else
                    {
                        returnMessage = handleMessage(retrievedMessage);
                    }
                }

                if (returnMessage == null)
                {
                    returnMessage = new MessageInfo("", "", retrievedMessage);
                }
            }
            return returnMessage;
        }

        /// <summary>
        /// Raw message logic that will parse through the supplied raw IRC message and returns
        /// MessageInfo object for further manipulation.
        /// </summary>
        /// <param name="message">Supplied raw IRC message</param>
        /// <returns>Message object</returns>
        public MessageInfo handleMessage(string message)
        {
            Debug.WriteLine(message);

            string user_id = "";
            string channel = "";
            string msg = "";
            MessageInfo theMessage = null;

            #region Regex Variables
            string tmi = @"tmi\.twitch\.tv\s";
            string timesThree = @"\:\w+!\w+@\w+\.";
            string channelHash = @"\#\w+";

            string badges = @"badges\=(\w*\/[0-9]*,?)*;";
            string colorReg = @"color\=(\#[0-9A-Za-z]{6})?;";
            string displayName = @"display\-name\=(\w+)?;";
            string emotes = @"emotes\=([0-9]+:(([0-9]*\-[0-9]*)([,||\/])*)*)*;";
            string emoteSets = @"emote\-sets\=([0-9]*,?)*;";
            string _id = @"(id=.*?;)?";
            string mod = @"mod\=[0,1];";
            string roomId = @"room\-id\=[0-9]+;";
            string subscriber = @"subscriber\=[0,1];";
            string tmiSent = @"(tmi\-sent\-ts\=[0-9]*;)?";
            string sentTs = @"(sent\-ts\=[0-9]*;)?";
            string turbo = @"turbo\=[0,1];";
            string userId = @"user\-id\=[0-9]+;";
            string userType = @"user\-type\=(\w*)?\s";
            string banDuration = @"ban\-duration\=[0-9]*;";
            string banReason = @"ban\-reason\=.*";
            string broadcasterLang = @"broadcaster\-lang\=(\w*)?;";
            string emoteOnly = @"emote\-only\=[0,1];";
            string followerOnly = @"followers\-only\=\-?[0-1]";
            string mercury = @"mercury\=[0,1];";
            string r9k = @"r9k\=[0,1];";
            string slow = @"slow\=[0,1];";
            string subsOnly = @"subs\-only\=[0,1]\s";
            string msgId = @"msg\-id\=\w*;";
            string paramMonths = @"msg\-param\-months\=[0-9]*;";
            string paramSubPlan = @"msg\-param\-sub\-plan\=\w *;";
            string paramSubPlanName = @"msg\-param\-sub\-plan\-name\=\w *;";
            string systemMsg = @"system\-msg\=.*?;";
            string login = @"login\=\w *;";
            string jtv = @":jtv\s";
            string bits = @"(bits\=.*;)?";
            //string tagPattern = @"\A@.*";

            string tmiPattern = $@".*{timesThree}{tmi}.*";
            string joinPattern = $@"\A{timesThree}{tmi}JOIN\s{channelHash}";
            string partPattern = $@"\A{timesThree}{tmi}PART\s{channelHash}";
            string privPattern = $@"(\A\@{badges}{bits}{colorReg}{displayName}{emotes}{_id}{mod}{roomId}{sentTs}{subscriber}{tmiSent}{turbo}{userId}{userType})?{timesThree}{tmi}PRIVMSG\s{channelHash}\s:.*";
            string modPattern = $@"\A{jtv}MODE\s{channelHash}\s\+o\s\w*\Z";
            string demodPattern = $@"\A{jtv}MODE\s{channelHash}\s\-o\s\w*\Z";
            string namesPattern = $@"(\A\:\w*\.{tmi}[0-9]*\s\w*\s=\s{channelHash}\s){1}\:(\w*\s?)*\Z";
            string clearPattern = $@"(\A\@{banDuration}?{banReason})?:{tmi}CLEARCHAT\s{channelHash}\s:\w*\Z";
            string globalPattern = $@"(\A\@{colorReg}{displayName}{emoteSets}{turbo}{userId}{userType})?{tmi}GLOBALUSERSTATE\Z";
            string roomstatePattern = $@"(\A\@{broadcasterLang}{emoteOnly}{followerOnly}{mercury}{r9k}{roomId}{slow}{subsOnly})?:{tmi}ROOMSTATE\s{channelHash}\Z";
            string usernoticePattern = $@"(\A\@{badges}{colorReg}{displayName}{emotes}{_id}{mod}{msgId}{paramMonths}{paramSubPlan}{paramSubPlanName}{roomId}{subscriber}{systemMsg}{login}{turbo}{userId}{userType})?:{tmi}USERNOTICE\s{channelHash}(\s:.*)?";
            string userstatePattern = $@"\A\@{colorReg}{displayName}{emoteSets}{mod}{subscriber}{turbo}{userType}:{tmi}USERSTATE\s{channelHash}\Z";
            #endregion

            if (Regex.IsMatch(message, tmiPattern)) //all messages containing ":<user>!<user>@<user>.tmi.twitch.tv"
            {
                try
                {
                    string lookUsername = $@"(?=(\w+\!\w+@\w+\.tmi\.twitch\.tv))\w+";
                    string lookChannel = $@"(?<=(tmi\.twitch\.tv\s\w+\s\#))\w+";
                    string lookMsg = $@"(?<=(tmi\.twitch\.tv\s\w+\s\#\w+\s:)).*";
                    string lookDisplayName = $@"(?<=(display\-name\=))[^;]*";
                    string lookMod = $@"(?<=(mod\=))[^;]*";
                    string lookUserId = $@"(?<=(user\-id\=))[^;]*";
                    user_id = Regex.Match(message, lookUsername).ToString();
                    channel = Regex.Match(message, lookChannel).ToString();
                    //user_id = msg.Substring(message.IndexOf(":") + 1, msg.IndexOf("!") - 1).Trim();
                    if (Regex.IsMatch(message, privPattern)) //PRIVMSG
                    {
                        msg = Regex.Match(message, lookMsg).ToString();
                        User newUser = new User(user_id);
                        if (message.Substring(0, 1) == "@")
                        {
                            string returnedString = Regex.Match(message, lookDisplayName).ToString();
                            if (returnedString != "") newUser.displayName = returnedString;
                            returnedString = Regex.Match(message, lookMod).ToString();
                            if (returnedString != "")
                            {
                                int returnedInt = int.Parse(returnedString);
                                if (returnedInt == 1) newUser.mod = true;
                            }
                            returnedString = Regex.Match(message, lookUserId).ToString();
                            if (returnedString != "")
                            {
                                int returnedInt = int.Parse(returnedString);
                                newUser.userId = returnedInt;
                            }
                            Debug.WriteLine($"{newUser.displayName} {newUser.mod} {newUser.userId}");
                        }
                        theMessage = new MessageInfo(newUser.name, channel, msg);
                        theMessage.header = IRCMessageType.PRIVMSG;
                        theMessage.theUser = newUser;
                    } //:<user>!<user>@<user>.tmi.twitch.tv JOIN #<channel>
                    else if (Regex.IsMatch(message, joinPattern))
                    {
                        theMessage = new MessageInfo(user_id, channel, "");
                        theMessage.header = IRCMessageType.JOIN;
                    }
                    else if (Regex.IsMatch(message, partPattern))
                    {
                        theMessage = new MessageInfo(user_id, channel, "");
                        theMessage.header = IRCMessageType.PART;
                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine($"UID - {user_id} Channel - {channel} msg - {msg}");
                }
            }
            else if (Regex.IsMatch(message, namesPattern))
            {

            }
            else if (message.Contains("jtv"))
            {
                string lookChannel = $@"(?<=(\w+\sMODE\s\#))\w+";
                string lookUsername = $@"(?<=(\w+\sMODE\s\#\w+\s[\+\-]o\s))\w+";
                try
                {
                    channel = Regex.Match(message, lookChannel).ToString();
                    user_id = Regex.Match(message, lookUsername).ToString();
                    theMessage = new MessageInfo(user_id, channel, "");
                    if (Regex.IsMatch(message, demodPattern))
                    {
                        theMessage.header = IRCMessageType.DEOP;
                        theMessage.theUser = new User(user_id);
                        theMessage.theUser.mod = false;
                    }
                    else if (Regex.IsMatch(message, modPattern))
                    {
                        theMessage.header = IRCMessageType.OP;
                        theMessage.theUser = new User(user_id);
                        theMessage.theUser.mod = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine("See handlemessage in IRCClient.cs");
                }
            }

            return theMessage;
        }

        /// <summary>
        /// Join Twitch channel via IRC.
        /// </summary>
        /// <param name="channel">The channel to join</param>
        public void joinRoom(string channel)
        {
            this.channel = channel;
            outputStream.WriteLine("JOIN #" + channel);
            outputStream.Flush();
        }

        /// <summary>
        /// Leave logic for the bot.
        /// </summary>
        /// <remarks>
        /// Needs logic to handle closing of channel tab on UI.
        /// </remarks>
        public void leaveRoom()
        {
            sendIRCMessage("PART #" + channel);
            outputStream.Close();
            inputStream.Close();
        }

        /// <summary>
        /// Will pull messages to send from queue to send.
        /// </summary>
        /// <remarks>
        /// Will need to recheck code to make sure it is using this method rather than
        /// bypassing it. Change sendChatMessage to private method to identify issues.
        /// Also add logic here to keep from sending messages that are not a part of bot
        /// channels.
        /// </remarks>
        public void sendMessage()
        {
            if (sendQ != null && !sendQ.IsEmpty)
            {
                string[] retrievedMessage;
                bool messageRetrieved = sendQ.TryDequeue(out retrievedMessage);
                if (messageRetrieved)
                {
                    sendChatMessage(retrievedMessage[0], retrievedMessage[1]);
                }
            }
        }

        /// <summary>
        /// Handle messages that are specific to the bot parameters.
        /// </summary>
        /// <param name="postMessage"></param>
        /// <returns></returns>
        public bool botLogic(out MessageInfo postMessage)
        {
            postMessage = null;
            if (chatMessageQ.Count > 0)
            {
                MessageInfo chatmsg;
                chatMessageQ.TryDequeue(out chatmsg);
                if (Regex.IsMatch(chatmsg.message, linkPattern))
                {
                    if (!chatmsg.theUser.isPermitted(chatmsg.channel))
                    {
                        string banMessage = $"/timeout {chatmsg.theUser.name} 1 Link detected. Please ask for permission first before posting.";
                        sendChatMessage(chatmsg.channel, banMessage);
                    }
                    chatmsg.ttsMessage = Regex.Replace(chatmsg.message, linkPattern, "hyperlink");
                    postMessage = chatmsg;
                    return true;
                }
                else if (Regex.IsMatch(chatmsg.message, staticCommand))
                {
                    if (commandDict[chatmsg.channel].ContainsKey(chatmsg.message.Substring(1)))
                    {
                        string response = commandDict[chatmsg.channel][chatmsg.message.Substring(1)];
                        sendQ.Enqueue(new string[2] { chatmsg.channel, response });
                        postMessage = chatmsg;
                        return true;
                    }
                    else if (chatmsg.message.Substring(1) == "reload" && chatmsg.theUser.isPermitted(chatmsg.channel))
                    {
                        Task.Factory.StartNew(() => loadlocalCommands(chatmsg.channel, commandsPath));
                        return false;
                    }
                }
                /*else if (Regex.IsMatch(chatmsg.message, dynamicCommand))
                {
                    Debug.WriteLine("Dynamic Command recognized.");
                    postMessage = chatmsg; //change this once this is worked out
                    return false;
                }*/
                bool changed = false;
                string[] messageArray = chatmsg.message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var groupedArray = messageArray.GroupBy(w => w);
                string tempMsg = " " + chatmsg.message;
                foreach (var item in groupedArray)
                {
                    if (item.Count() > 1)
                    {
                        Match matched = Regex.Match(tempMsg, $@"(\s{item.Key})\1+");
                        while (matched.Success)
                        {
                            tempMsg = Regex.Replace(tempMsg, matched.Value, $" {item.Key}");
                            matched = Regex.Match(tempMsg, $@"(\s{item.Key})\1");
                        }
                        changed = true;
                    }
                }
                messageArray = tempMsg.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (messageArray.Count() < ttsCountLimit)
                {
                    Regex repeatLetters = new Regex($@"([^\.])\1+");
                    foreach (string item in messageArray)
                    {
                        Match matched = repeatLetters.Match(item);
                        string holdItem = item;
                        while (matched.Success)
                        {
                            string replaceChars = matched.ToString().Substring(0, 2);
                            string replacedWord = Regex.Replace(holdItem, Regex.Escape(matched.Value), replaceChars);
                            tempMsg = Regex.Replace(tempMsg, Regex.Escape(holdItem), replacedWord);
                            holdItem = replacedWord;
                            matched = repeatLetters.Match(holdItem, matched.Index + 2);
                            changed = true;
                        }
                    }
                    if (tempMsg.Length < ttsCharLimit)
                    {
                        chatmsg.ttsMessage = tempMsg.Trim();
                        postMessage = chatmsg;
                        return changed;
                    }
                    else
                    {
                        postMessage = chatmsg;
                        return changed;
                    }
                }
                else
                {
                    postMessage = chatmsg;
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Attempt to load commands that are used in Twitch channel from specified txt file.
        /// </summary>
        /// <param name="channel">Referenced channel</param>
        /// <param name="path">Filepath to the commands</param>
        public void loadlocalCommands(string channel, string path)
        {
            string line;
            StreamReader file = null;
            try
            {
                file = new StreamReader(path);
                string resultPattern = $@"(?<=(;)).*";
                string commandPattern = $@"(?=(\w+;))\w+";

                if (!commandDict.ContainsKey(channel))
                {
                    commandDict.TryAdd(channel, new ConcurrentDictionary<string, string>());
                    Debug.WriteLine($"Added {channel} to commandDict.");
                }

                if (file != null)
                {
                    while ((line = file.ReadLine()) != null)
                    {
                        string command;
                        string result;
                        command = Regex.Match(line, commandPattern).ToString();
                        result = Regex.Match(line, resultPattern).ToString().Trim();
                        if (!commandDict[channel].ContainsKey(command))
                        {
                            commandDict[channel].TryAdd(command, result);
                        }
                        else
                        {
                            commandDict[channel].TryUpdate(command, result, commandDict[channel][command]);
                        }
                    }
                    file.Close();
                }
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException || e is DirectoryNotFoundException || e is IOException)
                {
                    onNoFileOpen?.Invoke(this, new OnNoFileOpenArgs { channel = channel, path = path, fileError = FileErrorType.NoCommandsFile });
                }
            }
        }

        /// <summary>
        /// Attempt to check database file for the specified users for past information.
        /// </summary>
        /// <param name="channel">Referenced channel</param>
        /// <param name="sqlListofUsers">List of users to check database for</param>
        public void loadDatabase(string channel, string sqlListofUsers)
        {
            if (!connDict.ContainsKey(channel))
            {
                onNoFileOpen?.Invoke(this, new OnNoFileOpenArgs { channel = channel, fileError = FileErrorType.NoDatabaseFile,
                    listofUsersSql = sqlListofUsers});
                return;
            }
            else
            {
                try
                {
                    Debug.WriteLine("sqlilistofusers is " + sqlListofUsers);

                    SqlCommand newCommand = new SqlCommand();
                    newCommand.Connection = connDict[channel];
                    newCommand.CommandText = "SELECT * FROM dbo.Users WHERE " + sqlListofUsers;
                    newCommand.Parameters.Add("@channel", SqlDbType.Text);
                    newCommand.Parameters["@channel"].Value = channel;
                    newCommand.Connection.Open();
                    AsyncCallback callback = new AsyncCallback(LoadDatabaseCallback);
                    CommandandChannel cc = new CommandandChannel() { theCommand = newCommand, channel = channel };
                    newCommand.BeginExecuteReader(callback, cc);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error code is " + e.HResult + " from the method " + e.TargetSite);
                    if (connDict[channel].State == ConnectionState.Open)
                    {
                        connDict[channel].Close();
                    }
                }
            }
        }

        /// <summary>
        /// Callback for loadDatabase method.
        /// </summary>
        /// <param name="result"></param>
        private void LoadDatabaseCallback(IAsyncResult result)
        {
            CommandandChannel retrievedcc = (CommandandChannel)result.AsyncState;
            //string channel = retrievedCommand.Parameters["@channel"].ToString();
            SqlCommand retrievedCommand = retrievedcc.theCommand;
            string theChannel = retrievedcc.channel;

            SqlDataReader dbData = retrievedCommand.EndExecuteReader(result);
            try
            {
                while(dbData.Read())
                {
                    //name, mod, id, points, modifier, alias, lastCheckIn
                    User tempUser = new User($"{dbData.GetString(0)}") { mod = dbData.GetBoolean(1), points = dbData.GetInt32(3),
                        lastCheckIn = dbData.GetDateTime(6), regular = dbData.GetBoolean(7), existsDB = true };
                    onDBActionArgs?.Invoke(this, new OnDBActionArgs() { channel = theChannel,
                        user = tempUser, dbActionType = DBActionType.LoadCall });
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace);
            }

            if (retrievedCommand.Connection.State == ConnectionState.Open)
            {
                retrievedCommand.Connection.Close();
            }
        }

        /// <summary>
        /// Helper object for when using AsyncCallback for loadDatabase method.
        /// </summary>
        private class CommandandChannel
        {
            public SqlCommand theCommand;
            public string channel;
        }
    }
}
