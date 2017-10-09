using System;
using Akiraionbot.Enums;

namespace Akiraionbot.Models
{
    /// <summary>
    /// This custom object allows for each message to be packaged uniformly
    /// for use across the application.
    /// </summary>
    class MessageInfo
    {
        public string channel { get; set; }
        public string message { get; set; }
        public string username { get; set; }
        public string ttsMessage { get; set; }
        public User theUser { get; set; }
        public IRCMessageType header { get; set; }

        public MessageInfo(string username, string channel, string message)
        {
            this.username = username;
            this.channel = channel;
            this.message = message;
            header = IRCMessageType.OTHER;
            theUser = new User(this.username) { lastCheckIn = DateTime.Now};
            ttsMessage = "";
        }
    }
}
