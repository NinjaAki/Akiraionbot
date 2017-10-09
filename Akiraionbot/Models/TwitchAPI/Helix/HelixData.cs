using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models.TwitchAPI.Helix
{
    /// <summary>
    /// JSON class data from Twitch Helix call. Refer to https://dev.twitch.tv/docs/api/reference
    /// </summary>
    class HelixData
    {
        public GenericHelixObject[] data;
        public Pagination pagination;

        public class GenericHelixObject
        {
            public int id;
            public string login;
            public string display_name;
            public string type;
            public string description;
            public string profile_image_url;
            public string offline_image_url;
            public int view_count;
            public string email;
            public int from_id;
            public int to_id;
            public DateTime followed_at;
            public int user_id;
            public int? game_id;
            public string[] community_ids;
            public string title;
            public DateTime started_at;
            public string language;
            public string thumbnail_url;
            public Metagame overwatch;
            public Metagame hearthstone;

            public User convertToUser()
            {
                User createdUser = null;
                if (login != null && login == "")
                {
                    createdUser = new User(login) { userId = id, followed_at = followed_at };
                }
                return createdUser;
            }
        }

        public class Pagination
        {
            public string cursor;
        }

        public class Metagame
        {
            public Broadcaster broadcaster;
            public Broadcaster opponent;
        }

        public class Broadcaster
        {
            public Hero hero;
        }
    }
}
