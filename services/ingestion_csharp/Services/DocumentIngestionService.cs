using Rbq.Contracts;

namespace Rbq.Ingestion;

/// <summary>Shared entry point for console ingestion and a future upload interface.</summary>
public sealed class DocumentIngestionService
{
    public async Task<ParsingResult> IngestAsync(string filePath, IngestionOptions options)
    {
        ValidateSettings(options);
        List<DocumentPage> pages = await ExtractPagesAsync(filePath);

        var chunker = new DocumentChunker();
        return chunker.CreateChunksWithReport(pages, options);
    }

    private static void ValidateSettings(IngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DocumentId))
        {
            throw new ArgumentException("DocumentId is required.");
        }

        if (options.SourceType == SourceType.Profile && string.IsNullOrWhiteSpace(options.ProfileId))
        {
            throw new ArgumentException("A competency profile needs a ProfileId.");
        }

        DocumentSource? source = options.Source;
        if (source is not null && source.Id != options.DocumentId)
        {
            throw new ArgumentException("DocumentId does not match the catalog entry.");
        }

        if (source is not null && options.Kind.HasValue && options.Kind.Value != source.Kind)
        {
            throw new ArgumentException("Document kind conflicts with the catalog entry.");
        }

        DocumentKind? selectedKind = source?.Kind ?? options.Kind;
        if (selectedKind.HasValue)
        {
            SourceType expectedType = selectedKind.Value switch
            {
                DocumentKind.CompetencyProfile => SourceType.Profile,
                DocumentKind.ExamInformation => SourceType.ExamInfo,
                _ => SourceType.Reference
            };

            if (options.SourceType != expectedType)
            {
                throw new ArgumentException("Source type does not match the document kind.");
            }
        }
    }

    private static async Task<List<DocumentPage>> ExtractPagesAsync(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var extractor = new PdfTextExtractor();
            return extractor.Extract(filePath);
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            string text = await File.ReadAllTextAsync(filePath);
            var page = new DocumentPage
            {
                Text = text,
                Number = 1,
                Filename = Path.GetFileName(filePath)
            };

            return new List<DocumentPage> { page };
        }

        throw new ArgumentException("Only PDF and UTF-8 TXT files are supported. Convert HTML sources explicitly.");
    }
}