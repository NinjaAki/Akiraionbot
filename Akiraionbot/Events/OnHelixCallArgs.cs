using System;
using Akiraionbot.Enums;
using Akiraionbot.Models.TwitchAPI.Helix;
using System.Collections.Generic;

namespace Akiraionbot.Events
{
    class OnHelixCallArgs : EventArgs
    {
        public HelixData response;
        public ApiCallType type;
        public string channel;
    }
}
