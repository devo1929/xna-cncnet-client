using System;
using Microsoft.Xna.Framework.Graphics;

namespace DTAClient.Online;

public class NotificationPopupMessage
{
    public Texture2D GameIcon { get; init; }
    public IRCUser IrcUser { get; init; }
    public string Message { get; init; }
}