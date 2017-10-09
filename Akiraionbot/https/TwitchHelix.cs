using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Akiraionbot.Events;
using Akiraionbot.Models.TwitchAPI.Helix;
using Newtonsoft.Json;
using Akiraionbot.Enums;
using System.Collections.Concurrent;

namespace Akiraionbot.https
{
    class TwitchHelix
    {
        string baseUrl = "https://api.twitch.tv/helix/";
        HttpClient httpClient = new HttpClient();
        public event EventHandler<OnHelixCallArgs> onHelixCallArgs;
        public ConcurrentQueue<HelixHelper> ApiQueue;
        public StringBuilder followBuilder { get; set; } = new StringBuilder();
        public string monitorChannel { get; set; } = "";
        public int rateRemaining = 30;

        /// <summary>
        /// Creates HttpClient for use.
        /// </summary>
        /// <param name="client_id">Specified client id</param>
        /// <param name="monitorChannel">Specified channel to monitor</param>
        public TwitchHelix(string client_id, string monitorChannel)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Client-ID", client_id);
            ApiQueue = new ConcurrentQueue<HelixHelper>();
            this.monitorChannel = monitorChannel;
        }

        /// <summary>
        /// Calls to Twitch Helix API and deseralizes to HelixData object.
        /// </summary>
        /// <param name="url">Specified url</param>
        /// <param name="channel">Target channel</param>
        /// <param name="callType">The API call type</param>
        /// <returns>Deserialized HelixData Object</returns>
        public async Task<HelixData> helixCall(string url, string channel, ApiCallType callType)
        {
            if (rateRemaining == 0)
            {
                Debug.WriteLine("Could not execute call. No more rate remaining.");
                return null;
            }
            else
            {
                string result = "";
                HelixData helixData = null;
                Debug.WriteLine($"Calling to {baseUrl}{url}");
                try
                {
                    using (HttpResponseMessage response = await httpClient.GetAsync(baseUrl + url))
                    using (HttpContent content = response.Content)
                    {
                        rateRemaining = Int32.Parse(response.Headers.GetValues("ratelimit-remaining").FirstOrDefault());
                        Debug.WriteLine($"Rate left: {rateRemaining}");
                        result = await content.ReadAsStringAsync();
                        Debug.WriteLine("Api call resulted in the following: " + result.Substring(0, 50) + "...");
                        helixData = JsonConvert.DeserializeObject<HelixData>(result);

                        if (helixData.data.Count() > 0 && callType != ApiCallType.NoCall)
                        {
                            onHelixCallArgs?.Invoke(this, new OnHelixCallArgs { response = helixData, type = callType, channel = channel });
                        }
                    }
                    return helixData;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.StackTrace);
                }
                return helixData;
            }
        }

        /// <summary>
        /// Attempts to dequeue HelixHelper object to run API call.
        /// </summary>
        public async void runApiCall()
        {
            if (ApiQueue.Count > 0)
            {
                HelixHelper apiInstance;
                if (ApiQueue.TryDequeue(out apiInstance))
                {
                    await helixCall(apiInstance.url, apiInstance.channel, apiInstance.callType);
                }

            }
            if (followBuilder.ToString() != "")
            {
                ApiQueue.Enqueue(new HelixHelper(followBuilder.ToString(), monitorChannel, ApiCallType.DoesFollowerCall));
            }
        }

        /// <summary>
        /// Sits in queue for API calls.
        /// </summary>
        public class HelixHelper
        {
            public string url;
            public ApiCallType callType;
            public string channel;

            public HelixHelper(string url, string channel, ApiCallType callType)
            {
                this.url = url;
                this.callType = callType;
                this.channel = channel;
            }
        }
    }
}
