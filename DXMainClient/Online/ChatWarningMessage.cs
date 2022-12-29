using System;
using Microsoft.Xna.Framework;

namespace DTAClient.Online;

public class ChatWarningMessage : ChatMessage
{
    public ChatWarningMessage(string senderName, DateTime dateTime, string message) : base(senderName, Color.Yellow, dateTime, PrefixMessage(message))
    {
    }

    public ChatWarningMessage(string senderName, string message) : base(senderName, Color.Yellow, PrefixMessage(message))
    {
    }

    public ChatWarningMessage(string senderName, string ident, bool senderIsAdmin, DateTime dateTime, string message) : base(senderName, ident, senderIsAdmin, Color.Yellow, dateTime, PrefixMessage(message))
    {
    }

    public ChatWarningMessage(string message) : base(Color.Yellow, PrefixMessage(message))
    {
    }

    private static string PrefixMessage(string message) => $"Warning: {message}";
}