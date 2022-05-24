using System.Text.RegularExpressions;
using BeepsOnBot.Models;

namespace BeepsOnBot.Services.TextParser;

public class Time24TextParser : ITextParser
{
    private const string Pattern =
        "^(?<hours>[0-2]{0,1}[0-9]{1}):?(?<minutes>[0-5]{1}[0-9]{1})?\\s*((?<hashtag>#{1})|$)\\s*(?<task>.*)?$";

    public string[] PossibleValues { get; } = {"12:30 #call someone special", "22:30 #sleep", "11:54", "23 (hours)"};

    public TextParseResult Parse(long chatId, string text, TimeZoneInfo tz)
    {
        var match = Regex.Match(text, Pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return TextParseResult.FalseResult;

        var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
        if (hours is > 24 or < 0)
            return TextParseResult.FalseResult;

        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;
        if (minutes is > 59 or < 0)
            return TextParseResult.FalseResult;

        var task = string.Empty;
        if (match.Groups["hashtag"].Success && match.Groups["task"].Success)
        {
            task = match.Groups["task"].Value.Trim();
        }

        var now = DateTime.UtcNow;
        var notifyAtUnspecified = new DateTime(now.Year,
            now.Month,
            now.Day,
            hours,
            minutes,
            0,
            DateTimeKind.Unspecified);
        var notifyAtUtc = TimeZoneInfo.ConvertTimeToUtc(notifyAtUnspecified, tz);
        
        while (notifyAtUtc < now)
        {
            notifyAtUtc = notifyAtUtc.AddDays(1);
        }

        return new TextParseResult(true, new TimerNotification(chatId, notifyAtUtc, task, text));
    }
}