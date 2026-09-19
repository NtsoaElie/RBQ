namespace Rbq.Ingestion.Parsers;

public interface IDocumentParser
{
    ParsingResult Parse(ExtractedDocument document, IngestionOptions options);
}
