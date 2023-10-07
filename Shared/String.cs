using System.Globalization;

namespace Shared;

public static class StringExtensions
{
    public static string ToTitleCase(this string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return title;
        }

        string[] words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Count(char.IsUpper) > 1 || words[i].Length <= 2)
            {
                continue;
            }
            if (i is 0 ||
                i == words.Length - 1 ||
                Separators.Any(x => words[i - 1].EndsWith(x)) ||
                !Exclusions.Contains(words[i].ToLowerInvariant()))
            {
                words[i] = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words[i].ToLowerInvariant());
            }
            else
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(" ", words);
    }

    private static readonly char[] Separators =
    {
        '-',
        ':',
        ';',
        '.',
        ',',
        '!',
        '?'
    };

    private static readonly HashSet<string> Exclusions = new()
    {
        "a",
        "an",
        "and",
        "at",
        "but",
        "by",
        "for",
        "in",
        "nor",
        "of",
        "on",
        "or",
        "so",
        "the",
        "to",
        "up",
        "yet",
        "with",
        "from"
    };
}
