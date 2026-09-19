using Rbq.Contracts;
using Rbq.Ingestion.Parsers;

namespace Rbq.Ingestion;

/// <summary>Coordinates extraction cleanup, parser selection and coverage validation.</summary>
public sealed class DocumentChunker
{
    public ParsingResult Parse(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DocumentId) || options.MaxCharacters < 100)
            throw new ArgumentException("DocumentId and a character budget of at least 100 are required.");
        if (options.SourceType == SourceType.Profile && string.IsNullOrWhiteSpace(options.ProfileId))
            throw new ArgumentException("A profile needs a ProfileId.");
        if (!Enum.IsDefined(options.SourceType))
            throw new ArgumentException("Invalid source type.");
        if (pages.Count == 0) return new ParsingResult();
        if (pages.Select(page => page.Filename).Distinct().Count() != 1 ||
            pages.Any(page => page.Number < 1) ||
            pages.Select(page => page.Number).Distinct().Count() != pages.Count ||
            !pages.Select(page => page.Number).SequenceEqual(pages.Select(page => page.Number).Order()))
            throw new ArgumentException("Process one document with unique, ascending page numbers.");

        if (options.Source is not null && (options.Source.Id != options.DocumentId ||
            (options.Kind.HasValue && options.Kind != options.Source.Kind)))
            throw new ArgumentException("Catalog identity or kind conflicts with ingestion options.");

        DocumentKind kind = options.Source?.Kind ?? options.Kind ?? options.SourceType switch
        {
            SourceType.Profile => DocumentKind.CompetencyProfile,
            SourceType.ExamInfo => DocumentKind.ExamInformation,
            _ => DocumentKind.TechnicalDocument
        };
        SourceType expectedType = kind switch
        {
            DocumentKind.CompetencyProfile => SourceType.Profile,
            DocumentKind.ExamInformation => SourceType.ExamInfo,
            _ => SourceType.Reference
        };
        if (options.SourceType != expectedType)
            throw new ArgumentException("Document kind and source type disagree.");

        var blocks = new List<TextBlock>();
        foreach (DocumentPage page in pages)
        {
            if (page.Blocks.Count > 0)
            {
                blocks.AddRange(page.Blocks);
                continue;
            }
            // Text files have no geometry. Do not guess which lines are page furniture.
            bool startsParagraph = true;
            int lineNumber = 0;
            foreach (string rawLine in page.Text.Split('\n'))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    startsParagraph = true;
                    continue;
                }
                blocks.Add(new TextBlock
                {
                    Id = $"{page.Number}:{lineNumber}",
                    Text = rawLine.Trim(),
                    PageNumber = page.Number,
                    StartsParagraph = startsParagraph
                });
                startsParagraph = false;
            }
        }

        var document = new ExtractedDocument
        {
            Source = options.Source ?? new DocumentSource
            {
                Id = options.DocumentId,
                Title = options.Title ?? Path.GetFileNameWithoutExtension(pages[0].Filename),
                Url = options.SourceUrl,
                Kind = kind,
                ProfileIds = string.IsNullOrWhiteSpace(options.ProfileId) ? [] : [options.ProfileId]
            },
            Filename = pages[0].Filename,
            Version = ChunkFactory.Hash(string.Join("\n", pages.Select(page => page.Text))),
            Blocks = blocks
        };

        HashSet<string> excludedIds = new HeaderFooterDetector().FindExcludedBlockIds(blocks);
        var cleaned = new ExtractedDocument
        {
            Source = document.Source,
            Filename = document.Filename,
            Version = document.Version,
            Blocks = blocks.Where(block => !excludedIds.Contains(block.Id)).ToList()
        };

        IDocumentParser parser = new DocumentParserFactory().GetParser(kind);
        ParsingResult result = parser.Parse(cleaned, options);
        foreach (TextBlock block in blocks.Where(block => excludedIds.Contains(block.Id)))
            result.Exclude(block, "repeated_margin_or_page_number");

        // Every block must appear in chunk provenance, exclusions or the review report.
        var accounted = result.Chunks.SelectMany(chunk => chunk.SourceBlockIds)
            .Concat(result.ExcludedBlocks.Select(item => item.Block.Id))
            .Concat(result.UnclassifiedBlocks.Select(block => block.Id)).ToHashSet();
        foreach (TextBlock block in blocks.Where(block => !accounted.Contains(block.Id)))
            result.UnclassifiedBlocks.Add(block);

        if (blocks.Any(block => block.Bounds is null))
            result.Warnings.Add("Some blocks have no layout coordinates; header/footer detection was not applied to them.");
        if (result.UnclassifiedBlocks.Count > 0)
        {
            result.Warnings.Add($"{result.UnclassifiedBlocks.Count} source blocks need classification.");
            foreach (Chunk chunk in result.Chunks)
                chunk.ReviewIssues.Add("unclassified_document_content");
        }
        if (result.Chunks.Count == 0)
            result.Warnings.Add("No chunks produced; this document needs review.");
        return result;
    }

    // Existing callers remain source-compatible; unresolved content flags travel with chunks.
    public List<Chunk> CreateChunks(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        return Parse(pages, options).Chunks;
    }
}
