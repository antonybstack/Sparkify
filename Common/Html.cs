using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Common;

public static class HtmlExtensions
{
    public static string HtmlToText(Stream htmlStream)
    {
        using var streamReader = new StreamReader(htmlStream);
        var htmlContent = streamReader.ReadToEnd();
        streamReader.Close();
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);
        var textContent = GetTextWithSpaces(doc.DocumentNode);
        return CleanupText(textContent);
    }

    private static string GetTextWithSpaces(HtmlNode node)
    {
        var text = new StringBuilder();
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                // Append the text content directly
                text.Append(child.InnerText);
            }
            else if (child.NodeType == HtmlNodeType.Element)
            {
                // Append a space before the element's text (if it's not the first child)
                if (child != node.FirstChild)
                {
                    text.Append(' ');
                }

                // Recursively append the child's text
                text.Append(GetTextWithSpaces(child));

                // Append a space after the element's text (if it's not the last child)
                if (child != node.LastChild)
                {
                    text.Append(' ');
                }
            }
        }

        return text.ToString();
    }

    private static string CleanupText(string text)
    {
        // Decode HTML entities
        text = WebUtility.HtmlDecode(text);

        // Remove extra spaces around punctuation
        text = Regex.Replace(text, @"\s+([,.!?;:])", "$1");

        // Replace multiple whitespaces (including new lines and tabs) with a single space
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }
}
