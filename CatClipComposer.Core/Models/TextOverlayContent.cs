using System.Globalization;
using System.Text;

namespace CatClipComposer.Core.Models;

public static class TextOverlayContent
{
    public static string NormalizeForRendering(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // drawtext treats CR and LF as separate line breaks. Normalizing Windows CRLF input to LF
        // keeps its multiline layout identical to WPF, which treats CRLF as one line break.
        var normalized = text
            .Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var result = new StringBuilder(normalized.Length);
        var hasBaseCharacter = false;
        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var isCombiningMark = category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            if (!isCombiningMark || hasBaseCharacter)
            {
                result.Append(rune.ToString());
            }

            if (!isCombiningMark)
            {
                hasBaseCharacter = category is not (
                    UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or
                    UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator or
                    UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or
                    UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or
                    UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation or
                    UnicodeCategory.OtherPunctuation);
            }
        }

        return result.ToString().TrimEnd('\n');
    }
}
