using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models.TwitchAPI.v5
{
    class Follow
    {
        public string created_at { get; set; }
        public bool notifications { get; set; }
        public TwitchUserv5 user { get; set; }
    }
}
