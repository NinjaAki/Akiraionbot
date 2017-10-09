using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models
{
    class TwitchUserv5
    {
        public string display_name { get; private set; }
        public int _id { get; private set; }
        public string name { get; private set; }
        public string type { get; private set; }
        public object bio { get; private set; }
        public DateTime created_at { get; private set; }
        public DateTime updated_at { get; private set; }
        public object logo { get; private set; }

        public TwitchUserv5(string display_name, int _id, string name, string type, object bio, DateTime created_at, DateTime updated_at, object logo)
        {
            this.display_name = display_name;
            this._id = _id;
            this.name = name;
            this.type = type;
            this.bio = bio;
            this.created_at = created_at;
            this.updated_at = updated_at;
            this.logo = logo;
        }

        override public string ToString()
        {
            return $"{display_name} {_id} {name} {type} {bio} {created_at} {updated_at} {logo}";
        }
    }
}
