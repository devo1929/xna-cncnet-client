using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using ReactiveUI;

namespace DTAClient.Online
{
    public class PrivateMessageUser : IEquatable<PrivateMessageUser>
    {
        public PrivateMessageUser(IRCUser ircUser, bool isOnline, bool isFriend)
        {
            IrcUser = ircUser;
            IsOnline = isOnline;
            IsFriend = isFriend;
        }
        
        public Texture2D GameIcon { get; set; }

        public IRCUser IrcUser { get; }

        public string Name => IrcUser?.Name;

        public bool IsOnline { get; set; }

        public bool IsFriend { get; set; }


        public bool Equals(PrivateMessageUser other)
            => !string.IsNullOrEmpty(Name) && Name == other?.Name;

        public override bool Equals(object obj) => Equals(obj as PrivateMessageUser);

        public override int GetHashCode() => HashCode.Combine(Name);
    }
}