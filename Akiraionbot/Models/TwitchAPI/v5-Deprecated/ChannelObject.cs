using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models.TwitchAPI.v5
{
    class ChannelObject
    {
        public int _total { get; set; }
        public string _cursor { get; set; }
        public List<Follow> follows;
    }
}
