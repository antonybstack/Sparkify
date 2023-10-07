using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Common;

public static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string RemoveInPlaceCharArray(string input)
    {
        var len = input.Length;
        var src = input.ToCharArray();
        var dstIdx = 0;
        for (var i = 0; i < len; i++)
        {
            var ch = src[i];
            switch (ch)
            {
                case '\u0020':
                case '\u00A0':
                case '\u1680':
                case '\u2000':
                case '\u2001':
                case '\u2002':
                case '\u2003':
                case '\u2004':
                case '\u2005':
                case '\u2006':
                case '\u2007':
                case '\u2008':
                case '\u2009':
                case '\u200A':
                case '\u202F':
                case '\u205F':
                case '\u3000':
                case '\u0009':
                case '\u000A': // LF
                case '\u000B': // VT
                case '\u000C': // FF
                case '\u000D': // CR
                case '\u0085': // NEL
                case '\u2028': // LS
                case '\u2029': // PS
                    if (dstIdx > 0 && src[dstIdx - 1] is ' ')
                    {
                        break;
                    }
                    src[dstIdx++] = ' ';
                    break;
                default:
                    if (char.IsAscii(ch))
                    {
                        src[dstIdx++] = ch;
                    }
                    break;
            }
        }
        return new string(src, 0, dstIdx);
    }

    public static string ToTitleCase(this string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return title;
        }

        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Count(char.IsUpper) > 1 || words[i].Length <= 2)
            {
                continue;
            }
            if (i is 0 ||
                i == words.Length - 1 ||
                !StopWords.Contains(words[i]) ||
                (char.IsAscii(words[i - 1][^1]) &&
                 (char.IsSeparator(words[i - 1][^1]) ||
                  char.IsPunctuation(words[i - 1][^1]))))
            {
                // capitalize first letter
                words[i] = string.Create(words[i].Length,
                    words[i],
                    static (span, word) =>
                    {
                        word.AsSpan().CopyTo(span);
                        span[0] = char.ToUpperInvariant(span[0]);
                    });
            }
        }
        // join with space
        return string.Create(words.Sum(static x => x.Length + 1) - 1,
            words,
            static (span, words) =>
            {
                var offset = 0;
                for (var i = 0; i < words.Length; i++)
                {
                    if (i > 0)
                    {
                        span[offset++] = ' ';
                    }
                    words[i].AsSpan().CopyTo(span[offset..]);
                    offset += words[i].Length;
                }
            });
    }

    public static readonly ReadOnlyMemory<char>[] StopWords =
        Array.ConvertAll(new[]
            {
                "a",
                "an",
                "and",
                "are",
                "as",
                "at",
                "be",
                "but",
                "by",
                "for",
                "if",
                "in",
                "into",
                "is",
                "it",
                "no",
                "not",
                "of",
                "on",
                "or",
                "such",
                "that",
                "the",
                "their",
                "then",
                "there",
                "these",
                "they",
                "this",
                "to",
                "was",
                "will",
                "with",
                "when"
            },
            static x => x.AsMemory());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains(this ReadOnlyMemory<char>[] arr, ReadOnlySpan<char> word)
    {
        for (var i = 0; i < arr.Length; i++)
        {
            if (arr[i].Span.CompareTo(word, StringComparison.OrdinalIgnoreCase) is 0)
            {
                return true;
            }
        }
        return false;
    }
}
