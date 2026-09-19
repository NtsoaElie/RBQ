using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
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

            // Match extracted characters to PDF glyphs without changing native text order.
            var glyphs = new List<(char Character, Letter Letter)>();
            foreach (Letter letter in page.Letters)
                foreach (char character in letter.Value)
                    if (!char.IsWhiteSpace(character))
                        glyphs.Add((character, letter));

            string extractedCharacters = new(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            string glyphCharacters = new(glyphs.Select(glyph => glyph.Character).ToArray());
            bool canLocate = extractedCharacters == glyphCharacters;
            int glyphOffset = 0;
            int lineNumber = 0;
            bool startsParagraph = true;
            var blocks = new List<TextBlock>();
            foreach (string rawLine in text.Split('\n'))
            {
                lineNumber++;
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    startsParagraph = true;
                    continue;
                }

                int characterCount = line.Count(c => !char.IsWhiteSpace(c));
                List<Letter> letters = canLocate
                    ? glyphs.Skip(glyphOffset).Take(characterCount).Select(glyph => glyph.Letter).ToList()
                    : [];
                glyphOffset += characterCount;

                TextBounds? bounds = null;
                double? fontSize = null;
                if (letters.Count > 0)
                {
                    bounds = new TextBounds
                    {
                        Left = letters.Min(letter => letter.BoundingBox.Left) / page.Width,
                        Right = letters.Max(letter => letter.BoundingBox.Right) / page.Width,
                        Top = 1 - letters.Max(letter => letter.BoundingBox.Top) / page.Height,
                        Bottom = 1 - letters.Min(letter => letter.BoundingBox.Bottom) / page.Height
                    };
                    fontSize = letters.Average(letter => letter.PointSize);
                }

                blocks.Add(new TextBlock
                {
                    Id = $"{page.Number}:{lineNumber}",
                    Text = line,
                    PageNumber = page.Number,
                    Bounds = bounds,
                    FontSize = fontSize,
                    StartsParagraph = startsParagraph
                });
                startsParagraph = false;
            }
            pages.Add(new DocumentPage
            {
                Text = text,
                Number = page.Number,
                Filename = Path.GetFileName(path),
                Blocks = blocks
            });
        }
        return pages;
    }
}
