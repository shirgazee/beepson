using BeepsOnBot.Models;

namespace BeepsOnBot.Services.TextParser;

public interface ITextParser
{
    static string[] PossibleValues { get; }

    TextParseResult Parse(long chatId, string text, TimeZoneInfo tz);
}