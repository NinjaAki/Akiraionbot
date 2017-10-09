using System;

namespace Akiraionbot.Models
{
    class TwitchUser
    {
        public int id { get; set; }
        public string login { get; set; }
        public string display_name { get; set; }
        public string type { get; set; }
        public string broadcaster_type { get; set; }
        public string description { get; set; }
        public Uri profile_image_url { get; set; }
        public Uri oflline_image_url { get; set; }
        public int view_count { get; set; }

        public TwitchUser(int id, string login, string display_name, string type, string broadcaster_type,
            string description, Uri profile_image_url, Uri offline_image_url, int view_count)
        {
            this.id = id;
            this.login = login;
            this.display_name = display_name;
            this.type = type;
            this.broadcaster_type = broadcaster_type;
            this.description = description;
            this.profile_image_url = profile_image_url;
            this.oflline_image_url = oflline_image_url;
            this.view_count = view_count;
        }
    }
}
