namespace DTAClient.Online;

public class PrivateMessage
{
    public ChatMessage ChatMessage { get; set; }
    
    public PrivateMessageUser User { get; set; }
    
    public bool ReceivedWhileInGame { get; set; }

    public PrivateMessage(ChatMessage chatMessage, PrivateMessageUser user)
    {
        ChatMessage = chatMessage;
        User = user;
    }
}