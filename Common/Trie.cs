using System.Text;

namespace Common;

public sealed class Trie
{
    private readonly Trie?[] _letters = new Trie[36];
    private bool _isWord;

    public Trie BuildTrie(string sentence)
    {
        var currentWord = new StringBuilder();
        foreach (var c in sentence)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                currentWord.Append(c);
            }
            else if (currentWord.Length > 0)
            {
                Insert(currentWord.ToString().ToLowerInvariant());
                currentWord.Clear();
            }
        }

        // add the last word if there is one
        if (currentWord.Length > 0)
        {
            Insert(currentWord.ToString().ToLowerInvariant());
        }

        return this;
    }

    private void Insert(string word)
    {
        var node = this;
        foreach (var c in word)
        {
            var index = GetIndex(c);
            if (node._letters[index] == null)
            {
                node._letters[index] = new Trie();
            }
            node = node._letters[index];
        }
        node._isWord = true;
    }

    /// <summary>
    /// Returns index of input string if IsWord is true, otherwise null
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public int? SearchLongestMatch(string input)
    {
        var node = this;
        var longestMatchIndex = 0;
        var currentMatchIndex = 0;
        while (currentMatchIndex < input.Length)
        {
            var index = GetIndex(input[currentMatchIndex]);
            if (index is -1 || node._letters[index] == null)
            {
                return longestMatchIndex;
            }
            node = node._letters[index];
            if (node._isWord)
            {
                longestMatchIndex = currentMatchIndex + 1;
            }
            currentMatchIndex++;
        }
        return longestMatchIndex;
    }

    private static int GetIndex(char c)
    {
        if (char.IsAsciiDigit(c))
        {
            return 26 + (c - '0'); // Mapping digits to the indices 26-35
        }
        if (char.IsAsciiLetter(c))
        {
            return char.ToLowerInvariant(c) - 'a'; // Assuming only lowercase letters
        }
        return -1;
    }
}
