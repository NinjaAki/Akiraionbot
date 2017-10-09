using Akiraionbot.Enums;
using Akiraionbot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Events
{
    class OnDBActionArgs : EventArgs
    {
        public string channel;
        public User user;
        public string listofUsersinSql;
        public DBActionType dbActionType;
    }
}
