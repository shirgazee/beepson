using System.Text.RegularExpressions;
using BeepsOnBot.Models;

namespace BeepsOnBot.Services.TextParser;

public class TimeSpanTextParser : ITextParser
{
    // (?<days>\\d+d)?\\s* - named group 'days' with 1 or more digits (\\d+) and 'd' in the end, ? - optional,
    // \\s* - any number of spaces
    // it seems that swift doesnt support conditions if-then, so for task we first check for a hashtag, then take the 'task' group
    // and for hashtag we check for # or "end of the line", because other text can lead to unpredictable errors
    // (copied from my swift code, didn't want to simplify. Also had to do TrimEnd stuff at the end of groups because of that)
    private const string Pattern =
        "^(?<days>\\d+d)?\\s*(?<hours>\\d+h)?\\s*(?<minutes>\\d+m)?\\s*(?<seconds>\\d+s)?\\s*((?<hashtag>#{1})|$)\\s*(?<task>.*)?$";

    public static string[] PossibleValues { get; } = {"1h 30m #study", "30s", "90s #breathe"};

    public TextParseResult Parse(long chatId, string text, TimeZoneInfo tz)
    {
        var match = Regex.Match(text, Pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return TextParseResult.FalseResult;

        var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value.TrimEnd('h')) : 0;
        if (hours < 0)
            return TextParseResult.FalseResult;

        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value.TrimEnd('m')) : 0;
        if (minutes is > 59 or < 0)
            return TextParseResult.FalseResult;
        
        var seconds = match.Groups["seconds"].Success ? int.Parse(match.Groups["seconds"].Value.TrimEnd('s')) : 0;
        if (seconds is > 59 or < 0)
            return TextParseResult.FalseResult;

        var task = string.Empty;
        if (match.Groups["hashtag"].Success && match.Groups["task"].Success)
        {
            task = match.Groups["task"].Value.Trim();
        }

        var totalSeconds = seconds + minutes * 60 + hours * 3600;
        if(totalSeconds == 0)
            return TextParseResult.FalseResult;
        
        var notifyAt = DateTime.UtcNow.AddSeconds(totalSeconds);

        return new TextParseResult(true, new TimerNotification(chatId, notifyAt, task, text));
    }
}