namespace Rbq.Ingestion.Parsers;

public sealed class DocumentParserFactory
{
    public IDocumentParser GetParser(DocumentKind kind)
    {
        return kind switch
        {
            DocumentKind.CompetencyProfile => new CompetencyProfileParser(),
            DocumentKind.LegalDocument => new LegalDocumentParser(),
            DocumentKind.ExamInformation => new ExamInformationParser(),
            DocumentKind.TechnicalDocument => new TechnicalDocumentParser(),
            _ => throw new ArgumentException("Unsupported document kind.")
        };
    }
}
