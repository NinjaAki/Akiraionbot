using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models.TwitchAPI.Helix
{
    class OWHero
    {
        string role { get; set; }
        string ability { get; set; }
        public string name { get; set; }
        List<DateTime> logTimes { get; set; }
        public Stopwatch playedTime { get; set; }

        public OWHero(string role, string ability, string name)
        {
            this.role = role;
            this.ability = ability;
            this.name = name;
            logTimes = new List<DateTime>();
            playedTime = new Stopwatch();
            this.activate();
        }

        public void deactivate()
        {
            playedTime.Stop();
        }

        public void activate()
        {
            DateTime currentTime = new DateTime();
            playedTime.Start();
            logTimes.Add(currentTime);
        }

        public string returnDuration()
        {
            TimeSpan ts = playedTime.Elapsed;
            return String.Format("{0:00}:{1:00}:{2:00}", ts.Hours,
                ts.Minutes, ts.Seconds);
        }
    }
}
