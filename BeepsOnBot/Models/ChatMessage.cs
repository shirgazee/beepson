namespace BeepsOnBot.Models;

public class ChatMessage
{
    public string Message  {get; private set;}
    public DateTime Sent {get; private set;}

    public ChatMessage(string message, DateTime sent)
    {
        Message = message;
        Sent = sent;
    }
}