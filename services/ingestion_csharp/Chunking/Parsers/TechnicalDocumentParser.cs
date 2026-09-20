using System.Text.RegularExpressions;

namespace Rbq.Ingestion.Parsers;

public sealed class TechnicalDocumentParser : SectionParser
{
    protected override bool IsHeading(TextBlock block, double bodyFontSize)
    {
        return Regex.IsMatch(block.Text, @"^(?:CHAPITRE|SECTION|PARTIE)\s+\S+", RegexOptions.IgnoreCase)
            || (block.Text.Length < 180 && bodyFontSize > 0 && block.FontSize > bodyFontSize * 1.2);
    }
}
