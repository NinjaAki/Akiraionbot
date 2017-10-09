using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Events.FollowerEvents
{
    class OnNewFollowerArgs : EventArgs
    {
        public string channel;
        public List<string> followerList;
    }
}
