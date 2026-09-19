using System.Text.RegularExpressions;

namespace Rbq.Ingestion.Parsers;

/// <summary>Shared paragraph handling for prose sources, without inventing competencies.</summary>
public abstract class SectionParser : IDocumentParser
{
    protected abstract bool IsHeading(TextBlock block, double bodyFontSize);

    public ParsingResult Parse(ExtractedDocument document, IngestionOptions options)
    {
        var result = new ParsingResult();
        var paragraphs = new List<TextBlock>();
        var sectionPath = new List<string>();
        double[] sizes = document.Blocks.Where(block => block.FontSize.HasValue)
            .Select(block => block.FontSize!.Value).Order().ToArray();
        double bodyFontSize = sizes.Length == 0 ? 0 : sizes[sizes.Length / 2];

        void Save()
        {
            if (paragraphs.Count == 0) return;
            result.Chunks.Add(ChunkFactory.Create(document, options, paragraphs, sectionPath: sectionPath));
            paragraphs = [];
        }

        foreach (TextBlock block in document.Blocks)
        {
            if (IsHeading(block, bodyFontSize))
            {
                Save();
                sectionPath = [block.Text];
                result.Exclude(block, "section_heading_context");
                continue;
            }
            // Never cut inside a paragraph, bullet sequence or an oversized semantic unit.
            bool bullet = Regex.IsMatch(block.Text, @"^(?:[•●\-]|o\s|\d+[.)])");
            int length = paragraphs.Sum(item => item.Text.Length + 1) + block.Text.Length;
            if (block.StartsParagraph && !bullet && length > options.MaxCharacters) Save();
            paragraphs.Add(block);
        }
        Save();
        return result;
    }
}
