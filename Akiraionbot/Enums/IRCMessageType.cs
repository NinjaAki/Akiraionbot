using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Enums
{
    enum IRCMessageType
    {
        PRIVMSG,
        PART,
        JOIN,
        OP,
        DEOP,
        NAMES,
        OTHER,
        FOLLOWER
    }
}
