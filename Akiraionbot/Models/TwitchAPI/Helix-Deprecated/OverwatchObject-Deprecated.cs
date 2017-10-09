using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models.TwitchAPI.Helix
{
    class OverwatchObject
    {
        public List<OWdata> data { get; set; }
        public Pagination pagination { get; set; }

        public class Pagination
        {
            public string cursor { get; set; }
        }
    }
}
