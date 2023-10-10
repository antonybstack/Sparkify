using System.Globalization;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Analysis.Core;
using System.Runtime.CompilerServices;

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
                char.IsSeparator(words[i - 1][^1]) ||
                char.IsPunctuation(words[i - 1][^1]) ||
                !StopWords.Contains(words[i]))
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

    public static readonly ReadOnlyMemory<char>[] StopWords =
        Array.ConvertAll(StopAnalyzer.ENGLISH_STOP_WORDS_SET.ToArray(), x => x.AsMemory());


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains(this ReadOnlyMemory<char>[] arr, ReadOnlySpan<char> word)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i].Span.CompareTo(word, StringComparison.OrdinalIgnoreCase) is 0)
            {
                return true;
            }
        }
        return false;
    }
}
