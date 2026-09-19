using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Rbq.Ingestion;

public sealed class PdfTextExtractor
{
    public List<DocumentPage> Extract(string path)
    {
        var pages = new List<DocumentPage>();
        using var document = PdfDocument.Open(path);

        foreach (var page in document.GetPages())
        {
            string text = ContentOrderTextExtractor.GetText(page, true);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"Page {page.Number} has no text. OCR/review is required.");

            pages.Add(new DocumentPage
            {
                Text = text,
                Number = page.Number,
                Filename = Path.GetFileName(path)
            });
        }

        return pages;
    }
}
