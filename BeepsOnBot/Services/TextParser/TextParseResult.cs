using BeepsOnBot.Models;

namespace BeepsOnBot.Services.TextParser;

public class TextParseResult
{
    public bool Parsed { get; private set; }
    
    public TimerNotification? Result { get; private set; }

    public TextParseResult(bool parsed, TimerNotification? result = null)
    {
        if (parsed && result == null)
            throw new InvalidOperationException($"{nameof(result)} should not be null when parsed");
                
        Parsed = parsed;
        Result = result;
    }

    public static TextParseResult FalseResult = new (false);
}