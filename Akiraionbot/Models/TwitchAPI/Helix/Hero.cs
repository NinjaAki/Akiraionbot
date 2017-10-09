using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Akiraionbot.Models.TwitchAPI.Helix
{
    /// <summary>
    /// Hero object that is returned as a part of the Meta Twitch Helix call.
    /// </summary>
    class Hero
    {
        public string role { get; set; }
        public string name { get; set; }
        public string ability { get; set; }
        public string type { get; set; }
        [JsonProperty(PropertyName = "class")]
        public string classType { get; set; }
        List<DateTime> logTimes { get; set; } = new List<DateTime>();
        public Stopwatch playedTime { get; set; } = new Stopwatch();

        public Hero()
        {
            this.activate();
        }

        public void deactivate()
        {
            playedTime.Stop();
        }

        public void activate()
        {
            DateTime currentTime = DateTime.Now;
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
