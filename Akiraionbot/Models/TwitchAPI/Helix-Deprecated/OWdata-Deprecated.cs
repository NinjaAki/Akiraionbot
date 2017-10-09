using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Akiraionbot.Models.TwitchAPI.Helix
{
    class OWdata
    {
        public int user_id { get; set; }
        public int? game_id { get; set; }
        public MetaGame overwatch { get; set; }
        public MetaGame hearthstone { get; set; }

        public class MetaGame
        {
            public Broadcaster broadcaster { get; set; }

            public class Broadcaster
            {
                public Hero hero { get; set; }

                public class Hero
                {
                    public string role { get; set; }
                    public string name { get; set; }
                    public string ability { get; set; }
                    public string type { get; set; }
                    [JsonProperty(PropertyName = "class")]
                    public string classType { get; set; }
                }
            }
        }

        public OWHero returnOWHero()
        {
            return new OWHero(this.overwatch.broadcaster.hero.role,
                this.overwatch.broadcaster.hero.ability,
                this.overwatch.broadcaster.hero.name);
        }
    }
}
