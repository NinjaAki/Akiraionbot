using Akiraionbot.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Events
{
    class OnNoFileOpenArgs : EventArgs
    {
        public string channel;
        public string path;
        public FileErrorType fileError;
        public string listofUsersSql;
    }
}
