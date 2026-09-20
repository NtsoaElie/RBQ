using Rbq.Contracts;
using Rbq.Ingestion.Parsers;

namespace Rbq.Ingestion;

/// <summary>Prepares document text, runs the appropriate parser, and reports unread content.</summary>
public sealed class DocumentChunker
{
    public ParsingResult CreateChunksWithReport(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        if (pages.Count == 0)
        {
            var emptyResult = new ParsingResult();
            emptyResult.Warnings.Add("No text extracted; this document needs review.");
            return emptyResult;
        }

        // 1. Gather the text and identify headers and footers to skip.
        List<TextBlock> allTextBlocks = CollectTextBlocks(pages);

        List<TextBlock> textToProcess = RemoveHeadersAndFooters(
            allTextBlocks, out HashSet<string> headerAndFooterIds);

        // 2. Let the parser for this document type create the chunks.
        ExtractedDocument document = PrepareDocumentForParser(pages, textToProcess, options);

        var parserFactory = new DocumentParserFactory();
        IDocumentParser documentParser = parserFactory.GetParser(document.Source.Kind);
        ParsingResult result = documentParser.Parse(document, options);

        // 3. Report skipped text, text the parser did not recognize, and warnings.
        RecordSkippedHeadersAndFooters(result, allTextBlocks, headerAndFooterIds);
        FindTextNotHandledByParser(result, allTextBlocks);
        AddReviewWarnings(result, allTextBlocks);

        return result;
    }

    // Kept for callers that only need chunks. Review issues remain attached to each chunk.
    public List<Chunk> CreateChunks(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        ParsingResult result = CreateChunksWithReport(pages, options);
        return result.Chunks;
    }

    private static ExtractedDocument PrepareDocumentForParser(
        IReadOnlyList<DocumentPage> pages,
        List<TextBlock> textToProcess,
        IngestionOptions options)
    {
        string filename = pages[0].Filename;
        DocumentKind documentKind = DetermineDocumentKind(options);
        DocumentSource source = GetDocumentSource(filename, documentKind, options);
        string documentVersion = CreateDocumentVersion(pages);

        return new ExtractedDocument
        {
            Source = source,
            Filename = filename,
            Version = documentVersion,
            Blocks = textToProcess
        };
    }

    private static string CreateDocumentVersion(IReadOnlyList<DocumentPage> pages)
    {
        // Use all original text, including skipped headers and footers.
        // The same document text produces the same version.
        string originalText = string.Join("\n", pages.Select(page => page.Text));
        return ChunkFactory.Hash(originalText);
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

    private static List<TextBlock> RemoveHeadersAndFooters(
        List<TextBlock> allTextBlocks,
        out HashSet<string> removedBlockIds)
    {
        var headerFooterDetector = new HeaderFooterDetector();
        removedBlockIds = headerFooterDetector.FindExcludedBlockIds(allTextBlocks);

        var contentBlocks = new List<TextBlock>();

        foreach (TextBlock block in allTextBlocks)
        {
            bool isDetectedHeaderOrFooter = removedBlockIds.Contains(block.Id);

            if (isDetectedHeaderOrFooter)
            {
                continue;
            }

            contentBlocks.Add(block);
        }

        // Keep the original blocks intact so removed text can appear in the report.
        return contentBlocks;
    }

    private static void RecordSkippedHeadersAndFooters(
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

    private static void FindTextNotHandledByParser(ParsingResult result, List<TextBlock> blocks)
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