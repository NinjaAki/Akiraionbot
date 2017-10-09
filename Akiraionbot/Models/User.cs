using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akiraionbot.Models
{
    /// <summary>
    /// Merges facets of Twitch IRC information and necessary database properties of a user.
    /// </summary>
    class User
    {
        public string badge { get; set; } = "";
        public int badgeVersion { get; set; } = 0;
        public string bits { get; set; } = "";
        public Color color { get; set; } = Color.White;
        public string displayName { get; set; } = "";
        public bool mod { get; set; } = false;
        public bool subscriber { get; set; } = false;
        public bool turbo { get; set; } = false;
        public int userId { get; set; } = 0;
        public string userType { get; set; } = "";
        public string name { get; } = "";
        public DateTime? followed_at { get; set; } = null;
        public DateTime? lastCheckIn { get; set; } = null;
        public string Forward { get; set; } = "";
        public string Back { get; set; } = "";
        public bool checkedDB { get; set; } = false;
        public bool existsDB { get; set; } = false;
        public bool regular { get; set; } = false;
        public int points { get; set; } = 0;
        public int modifier { get; set; } = 1;
        public string alias { get; set; } = "";

        public static string modSym = @"+";

        /// <summary>
        /// Default constructor. Name is a required key.
        /// </summary>
        /// <param name="name">Specified username</param>
        public User(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Will attempt to update properties from the newUser but will keep certain
        /// properties that require a second check such as -op command from IRC as
        /// this could signify that the user left the channel rather than a full
        /// demod.
        /// </summary>
        /// <remarks>
        /// At the moment, demod requires hardsetting the mod property back to false if
        /// a demod happens.
        /// </remarks>
        /// <param name="oldUser">An instance of a user</param>
        /// <param name="newUser">A new instance of a user</param>
        /// <param name="theUser">Returned updated user</param>
        /// <returns>True if oldUser has been changed.</returns>
        public static bool lazyUpdate(User oldUser, User newUser, out User theUser)
        {
            bool differentBool = false;
            if (newUser.displayName != "")
            {
                if (newUser.displayName != oldUser.displayName)
                {
                    oldUser.displayName = newUser.displayName;
                    differentBool = true;
                }

            }
            if (!oldUser.mod)
            {
                oldUser.mod = newUser.mod;
                differentBool = true;
            }
            if (newUser.userId != 0 && newUser.userId != oldUser.userId)
            {
                oldUser.userId = newUser.userId != oldUser.userId ? newUser.userId : oldUser.userId;
                differentBool = true;
            }
            if (oldUser.followed_at == null && newUser.followed_at != null)
            {
                oldUser.followed_at = newUser.followed_at;
                differentBool = true;
            }
            if (!oldUser.checkedDB)
            {
                if (newUser.checkedDB) oldUser.checkedDB = true;
                differentBool = true;
            }
            if (!oldUser.existsDB)
            {
                if (newUser.existsDB) oldUser.existsDB = true;
                differentBool = true;
            }
            if (newUser.lastCheckIn != null)
            {
                if (oldUser.lastCheckIn != null)
                {
                    int tempInt = DateTime.Compare((DateTime)oldUser.lastCheckIn, (DateTime)newUser.lastCheckIn);
                    if (tempInt < 0)
                    {
                        oldUser.lastCheckIn = newUser.lastCheckIn;
                        differentBool = true;
                    }
                }
                else
                {
                    oldUser.lastCheckIn = newUser.lastCheckIn;
                    differentBool = true;
                }
            }
            if (oldUser.points < newUser.points)
            {
                oldUser.points = newUser.points;
                differentBool = true;
            }
            if (newUser.regular) {
                if (!oldUser.regular) oldUser.regular = newUser.regular;
            }
            theUser = oldUser;
            return differentBool;
        }

        /// <summary>
        /// This will merge properties of the referenced users with newUser property
        /// priority.
        /// </summary>
        /// <param name="oldUser">An instance of the user</param>
        /// <param name="newUser">A new instance of the user</param>
        /// <param name="theUser">Returned updated user</param>
        public static void forceUpdate(User oldUser, User newUser, out User theUser)
        {
            if (newUser.displayName != "")
            {
                oldUser.displayName = newUser.displayName;
            }
            if (newUser.mod != oldUser.mod)
            {
                oldUser.mod = newUser.mod;
            }
            if (newUser.userId != 0 && newUser.userId != oldUser.userId)
            {
                oldUser.userId = newUser.userId;
            }
            if (newUser.followed_at != oldUser.followed_at)
            {
                oldUser.followed_at = newUser.followed_at;
            }
            theUser = oldUser;
        }

        /// <summary>
        /// Checks if a user is "permitted" to post links, spam channel, etc.
        /// </summary>
        /// <param name="channel">Specified channel</param>
        /// <returns>True if user is permitted</returns>
        public bool isPermitted(string channel)
        {
            if (mod || name == channel || regular) return true;
            return false;
        }

        /// <summary>
        /// The display name on the UI.
        /// </summary>
        /// <returns>Display name</returns>
        public override string ToString()
        {
            string theMod = mod ? modSym : "";
            return $@"{theMod}{name}";
        }
    }
}
