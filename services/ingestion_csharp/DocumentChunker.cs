using Rbq.Contracts;
using Rbq.Ingestion.Parsers;

namespace Rbq.Ingestion;

/// <summary>Prepares document text, runs the appropriate parser, and reports unread content.</summary>
public sealed class DocumentChunker
{
    public ParsingResult CreateChunksWithReport(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        ValidateOptions(options);

        if (pages.Count == 0)
        {
            return new ParsingResult();
        }

        ValidatePages(pages);
        ValidateCatalogOptions(options);

        DocumentKind kind = DetermineDocumentKind(options);
        ValidateSourceType(kind, options.SourceType);

        List<TextBlock> blocks = CollectTextBlocks(pages);
        DocumentSource source = GetDocumentSource(pages[0].Filename, kind, options);

        // Keep the version based on all original text, including headers and footers.
        string documentText = string.Join("\n", pages.Select(page => page.Text));
        string documentVersion = ChunkFactory.Hash(documentText);

        var headerFooterDetector = new HeaderFooterDetector();
        HashSet<string> excludedBlockIds = headerFooterDetector.FindExcludedBlockIds(blocks);
        List<TextBlock> contentBlocks = GetContentBlocks(blocks, excludedBlockIds);

        var document = new ExtractedDocument
        {
            Source = source,
            Filename = pages[0].Filename,
            Version = documentVersion,
            Blocks = contentBlocks
        };

        var parserFactory = new DocumentParserFactory();
        IDocumentParser parser = parserFactory.GetParser(kind);
        ParsingResult result = parser.Parse(document, options);

        RecordExcludedBlocks(result, blocks, excludedBlockIds);
        FindUnclassifiedBlocks(result, blocks);
        AddReviewWarnings(result, blocks);

        return result;
    }

    // Kept for callers that only need chunks. Review issues remain attached to each chunk.
    public List<Chunk> CreateChunks(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        ParsingResult result = CreateChunksWithReport(pages, options);
        return result.Chunks;
    }

    private static void ValidateOptions(IngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DocumentId))
        {
            throw new ArgumentException("DocumentId is required.");
        }

        if (options.MaxCharacters < 100)
        {
            throw new ArgumentException("The character budget must be at least 100.");
        }

        if (options.SourceType == SourceType.Profile && string.IsNullOrWhiteSpace(options.ProfileId))
        {
            throw new ArgumentException("A profile needs a ProfileId.");
        }

        if (!Enum.IsDefined(options.SourceType))
        {
            throw new ArgumentException("Invalid source type.");
        }
    }

    private static void ValidatePages(IReadOnlyList<DocumentPage> pages)
    {
        string filename = pages[0].Filename;
        int previousPageNumber = 0;

        foreach (DocumentPage page in pages)
        {
            if (page.Filename != filename)
            {
                throw new ArgumentException("Process one document at a time.");
            }

            // Starting at zero also ensures that the first page number is positive.
            if (page.Number <= previousPageNumber)
            {
                throw new ArgumentException("Page numbers must be positive, unique, and ascending.");
            }

            previousPageNumber = page.Number;
        }
    }

    private static void ValidateCatalogOptions(IngestionOptions options)
    {
        if (options.Source is null)
        {
            return;
        }

        if (options.Source.Id != options.DocumentId)
        {
            throw new ArgumentException("Catalog identity conflicts with DocumentId.");
        }

        if (options.Kind.HasValue && options.Kind.Value != options.Source.Kind)
        {
            throw new ArgumentException("Catalog kind conflicts with the requested document kind.");
        }
    }

    private static DocumentKind DetermineDocumentKind(IngestionOptions options)
    {
        if (options.Source is not null)
        {
            return options.Source.Kind;
        }

        if (options.Kind.HasValue)
        {
            return options.Kind.Value;
        }

        switch (options.SourceType)
        {
            case SourceType.Profile:
                return DocumentKind.CompetencyProfile;
            case SourceType.ExamInfo:
                return DocumentKind.ExamInformation;
            default:
                return DocumentKind.TechnicalDocument;
        }
    }

    private static void ValidateSourceType(DocumentKind kind, SourceType sourceType)
    {
        SourceType expectedSourceType;

        switch (kind)
        {
            case DocumentKind.CompetencyProfile:
                expectedSourceType = SourceType.Profile;
                break;
            case DocumentKind.ExamInformation:
                expectedSourceType = SourceType.ExamInfo;
                break;
            default:
                expectedSourceType = SourceType.Reference;
                break;
        }

        if (sourceType != expectedSourceType)
        {
            throw new ArgumentException("Document kind and source type disagree.");
        }
    }

    private static List<TextBlock> CollectTextBlocks(IReadOnlyList<DocumentPage> pages)
    {
        var blocks = new List<TextBlock>();

        foreach (DocumentPage page in pages)
        {
            if (page.Blocks.Count > 0)
            {
                blocks.AddRange(page.Blocks);
            }
            else
            {
                blocks.AddRange(CreateBlocksFromPlainText(page));
            }
        }

        return blocks;
    }

    private static List<TextBlock> CreateBlocksFromPlainText(DocumentPage page)
    {
        // Plain text has no page coordinates, so margin detection cannot use these blocks.
        var blocks = new List<TextBlock>();
        bool startsParagraph = true;
        int lineNumber = 0;

        foreach (string line in page.Text.Split('\n'))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                startsParagraph = true;
                continue;
            }

            var block = new TextBlock
            {
                Id = $"{page.Number}:{lineNumber}",
                Text = line.Trim(),
                PageNumber = page.Number,
                StartsParagraph = startsParagraph
            };

            blocks.Add(block);
            startsParagraph = false;
        }

        return blocks;
    }

    private static DocumentSource GetDocumentSource(
        string filename,
        DocumentKind kind,
        IngestionOptions options)
    {
        if (options.Source is not null)
        {
            return options.Source;
        }

        var profileIds = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.ProfileId))
        {
            profileIds.Add(options.ProfileId);
        }

        return new DocumentSource
        {
            Id = options.DocumentId,
            Title = options.Title ?? Path.GetFileNameWithoutExtension(filename),
            Url = options.SourceUrl,
            Kind = kind,
            ProfileIds = profileIds
        };
    }

    private static List<TextBlock> GetContentBlocks(
        List<TextBlock> blocks,
        HashSet<string> excludedBlockIds)
    {
        var contentBlocks = new List<TextBlock>();

        foreach (TextBlock block in blocks)
        {
            if (!excludedBlockIds.Contains(block.Id))
            {
                contentBlocks.Add(block);
            }
        }

        return contentBlocks;
    }

    private static void RecordExcludedBlocks(
        ParsingResult result,
        List<TextBlock> blocks,
        HashSet<string> excludedBlockIds)
    {
        foreach (TextBlock block in blocks)
        {
            if (excludedBlockIds.Contains(block.Id))
            {
                result.Exclude(block, "repeated_margin_or_page_number");
            }
        }
    }

    private static void FindUnclassifiedBlocks(ParsingResult result, List<TextBlock> blocks)
    {
        // Every original block must be in a chunk, explicitly excluded, or marked for review.
        var accountedBlockIds = new HashSet<string>();

        foreach (Chunk chunk in result.Chunks)
        {
            accountedBlockIds.UnionWith(chunk.SourceBlockIds);
        }

        foreach (var excludedBlock in result.ExcludedBlocks)
        {
            accountedBlockIds.Add(excludedBlock.Block.Id);
        }

        foreach (TextBlock block in result.UnclassifiedBlocks)
        {
            accountedBlockIds.Add(block.Id);
        }

        foreach (TextBlock block in blocks)
        {
            if (!accountedBlockIds.Contains(block.Id))
            {
                result.UnclassifiedBlocks.Add(block);
            }
        }
    }

    private static void AddReviewWarnings(ParsingResult result, List<TextBlock> blocks)
    {
        bool hasMissingCoordinates = blocks.Any(block => block.Bounds is null);

        if (hasMissingCoordinates)
        {
            result.Warnings.Add("Some blocks have no layout coordinates; header/footer detection was not applied to them.");
        }

        if (result.UnclassifiedBlocks.Count > 0)
        {
            result.Warnings.Add($"{result.UnclassifiedBlocks.Count} source blocks need classification.");

            foreach (Chunk chunk in result.Chunks)
            {
                chunk.ReviewIssues.Add("unclassified_document_content");
            }
        }

        if (result.Chunks.Count == 0)
        {
            result.Warnings.Add("No chunks produced; this document needs review.");
        }
    }
}