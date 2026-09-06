using System.Text;

namespace Primer.Shared.Presentation;

/// <summary>
/// Renders a payload as human-oriented text, wrapped to the detected width and reduced to
/// ASCII when the console encoding cannot carry the original characters.
/// </summary>
internal sealed class TextOutputFormatter(ITerminalCapabilities capabilities) : IOutputFormatter
{
    /// <summary>
    /// Substitutes for the non-ASCII characters the tool emits. A console that cannot
    /// represent a character shows a readable stand-in rather than a replacement glyph.
    /// </summary>
    private static readonly (char Original, string Substitute)[] AsciiSubstitutes =
    [
        ('\u2026', "..."),
        ('\u2192', "->"),
        ('\u2713', "ok"),
        ('\u2717', "x"),
        ('\u2022', "-"),
        ('\u2014', "--"),
        ('\u2018', "'"),
        ('\u2019', "'"),
        ('\u201c', "\""),
        ('\u201d', "\""),
    ];

    public string Render(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var text = payload switch
        {
            string single => single,
            IEnumerable<string> lines => string.Join('\n', lines),
            _ => payload.ToString() ?? string.Empty,
        };

        if (!capabilities.SupportsUnicode)
        {
            text = ToAscii(text);
        }

        return Wrap(text, capabilities.Width);
    }

    private static string ToAscii(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            var substitute = Array.Find(AsciiSubstitutes, entry => entry.Original == character);

            if (substitute.Substitute is not null)
            {
                builder.Append(substitute.Substitute);
            }
            else if (character < 128)
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }

    private static string Wrap(string text, int width)
    {
        var wrapped = new StringBuilder(text.Length);

        foreach (var paragraph in text.Split('\n'))
        {
            if (wrapped.Length > 0)
            {
                wrapped.Append('\n');
            }

            WrapParagraph(paragraph, width, wrapped);
        }

        return wrapped.ToString();
    }

    private static void WrapParagraph(string paragraph, int width, StringBuilder wrapped)
    {
        if (paragraph.Length <= width)
        {
            wrapped.Append(paragraph);
            return;
        }

        var column = 0;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (column > 0 && column + 1 + word.Length > width)
            {
                wrapped.Append('\n');
                column = 0;
            }
            else if (column > 0)
            {
                wrapped.Append(' ');
                column++;
            }

            wrapped.Append(word);
            column += word.Length;
        }
    }
}
