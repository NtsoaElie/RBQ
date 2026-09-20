using System.Text.RegularExpressions;

namespace Rbq.Ingestion.Parsers;

/// <summary>Conservative parser for explicit French article headings; unknown material is reported.</summary>
public sealed class LegalDocumentParser : IDocumentParser
{
    private static readonly Regex Article = new(@"^(?:ARTICLE|Article)\s+(\d+(?:\.\d+)*)(?:[.\s:]|$)");

    public ParsingResult Parse(ExtractedDocument document, IngestionOptions options)
    {
        var result = new ParsingResult();
        var articleBlocks = new List<TextBlock>();
        var sections = new List<string>();
        string? articleNumber = null;

        void SaveArticle()
        {
            if (articleBlocks.Count == 0) return;
            result.Chunks.Add(ChunkFactory.Create(document, options, articleBlocks,
                sectionPath: sections, articleNumber: articleNumber));
            articleBlocks = [];
        }

        foreach (TextBlock block in document.Blocks)
        {
            if (Regex.IsMatch(block.Text, @"^(CHAPITRE|SECTION|PARTIE)\s+\S+", RegexOptions.IgnoreCase))
            {
                SaveArticle();
                articleNumber = null;
                if (block.Text.StartsWith("CHAPITRE", StringComparison.OrdinalIgnoreCase) ||
                    block.Text.StartsWith("PARTIE", StringComparison.OrdinalIgnoreCase)) sections.Clear();
                else if (sections.Count > 1) sections.RemoveRange(1, sections.Count - 1);
                sections.Add(block.Text);
                result.Exclude(block, "legal_heading_context");
                continue;
            }
            Match match = Article.Match(block.Text);
            if (match.Success)
            {
                SaveArticle();
                articleNumber = match.Groups[1].Value;
            }
            if (articleNumber is null) result.UnclassifiedBlocks.Add(block);
            else articleBlocks.Add(block);
        }
        SaveArticle();
        if (result.Chunks.Count == 0)
            result.Warnings.Add("No explicit Article headings found. Use a source-specific legal adapter; generic numbered paragraphs are not assumed to be articles.");
        return result;
    }
}
