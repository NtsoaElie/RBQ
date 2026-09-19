namespace Rbq.Ingestion.Parsers;

public sealed class ExamInformationParser : SectionParser
{
    protected override bool IsHeading(TextBlock block, double bodyFontSize)
    {
        return block.Text.Length < 180 && bodyFontSize > 0 && block.FontSize > bodyFontSize * 1.15;
    }
}
