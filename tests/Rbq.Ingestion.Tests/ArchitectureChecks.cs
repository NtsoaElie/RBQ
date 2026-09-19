using Rbq.Contracts;
using Rbq.Ingestion;
using Rbq.Ingestion.Parsers;

public static class ArchitectureChecks
{
    public static void Run(Action<bool, string> check)
    {
        TextBlock Block(string text, int page, int line, double top) => new()
        {
            Id = $"{page}:{line}",
            Text = text,
            PageNumber = page,
            FontSize = 11,
            Bounds = new TextBounds { Left = 0.1, Right = 0.8, Top = top, Bottom = top + 0.01 }
        };
        var margins = new List<TextBlock>();
        for (int page = 1; page <= 3; page++)
        {
            margins.Add(Block("Titre arbitraire du document", page, 1, 0.02));
            margins.Add(Block("Une obligation répétée", page, 2, 0.50));
            margins.Add(Block("Module 1 – Prévention", page, 3, 0.04));
            margins.Add(Block(page.ToString(), page, 4, 0.95));
            margins.Add(Block("1000", page, 5, 0.60));
        }
        var excluded = new HeaderFooterDetector().FindExcludedBlockIds(margins);
        check(excluded.Count == 6, "Only stable repeated margins and page numbers removed");
        check(!excluded.Contains("1:2") && !excluded.Contains("1:3") && !excluded.Contains("1:5"),
            "Repeated body text, module headers and body numbers preserved");

        var options = new IngestionOptions
        {
            DocumentId = "test",
            ProfileId = "TEST",
            SourceType = SourceType.Profile
        };
        DocumentPage Page(string text) => new() { Text = text, Number = 1, Filename = "test.txt" };
        var chunker = new DocumentChunker();
        string input = "Module 1 – Un titre\nsur deux lignes\nÉléments de compétence Habiletés minimalement requises\n2. Comprendre\n2.1. Appliquer les règles\nselon la loi (RLRQ, c. A-\n3.001, art. 4),\n199 et 200).\n2.1 art. 12 à 31).\n2.2. Expliquer.";
        var result = chunker.CreateChunksWithReport([Page(input)], options);
        check(result.Chunks.Count == 2 && result.Chunks.All(chunk => chunk.ReviewIssues.Count == 0),
            "Wrapped citations do not create false skills or competencies");
        check(result.Chunks[0].Module!.Label == "Un titre sur deux lignes", "Multiline module title retained");
        check(result.Chunks[0].Skill!.Label.Contains("selon la loi"), "Wrapped skill label retained");
        check(result.Chunks[0].Content.Contains("199 et 200"), "Numeric source content preserved");
        int accounted = result.Chunks.Sum(chunk => chunk.SourceBlockIds.Count)
            + result.ExcludedBlocks.Count + result.UnclassifiedBlocks.Count;
        check(accounted == input.Split('\n').Length, "Every source block is accounted for");

        var unknown = chunker.CreateChunksWithReport([Page("1.1. Une compétence sans module")], options);
        check(unknown.Chunks.Count == 0 && unknown.UnclassifiedBlocks.Count == 1 && unknown.Warnings.Count > 0,
            "Unknown profile layout retains unclassified source");
        var noLayout = chunker.CreateChunksWithReport([Page("Titre quelconque\n1000")], new IngestionOptions
        {
            DocumentId = "ref", SourceType = SourceType.Reference, SkillIds = ["TEST:skill:2.1"]
        });
        check(noLayout.Chunks.Single().Content.Contains("1000"), "Text input does not guess page numbers");

        var legalOptions = new IngestionOptions
        {
            DocumentId = "law", SourceType = SourceType.Reference, Kind = DocumentKind.LegalDocument,
            SkillIds = ["TEST:skill:2.1"]
        };
        var law = chunker.CreateChunksWithReport([Page("CHAPITRE I\nSECTION 1\nArticle 12. Une obligation.\nSauf dans le cas suivant :\n• Une exception.\nArticle 13. Une autre obligation.")], legalOptions);
        check(law.Chunks.Count == 2 && law.Chunks[0].ArticleNumber == "12", "Legal articles have source structure, not competencies");
        check(law.Chunks[0].Content.Contains("Une exception") && law.Chunks[0].Skill is null,
            "Article conditions and exceptions kept together");
        check(law.Chunks[0].SectionPath.SequenceEqual(new[] { "CHAPITRE I", "SECTION 1" }), "Legal hierarchy retained");
        var unsupportedLaw = chunker.CreateChunksWithReport([Page("12. Une disposition sans balisage")], legalOptions);
        check(unsupportedLaw.UnclassifiedBlocks.Count == 1 && unsupportedLaw.Chunks.Count == 0,
            "Ambiguous legal numbering is reported instead of guessed");
        var factory = new DocumentParserFactory();
        check(factory.GetParser(DocumentKind.ExamInformation) is ExamInformationParser &&
            factory.GetParser(DocumentKind.TechnicalDocument) is TechnicalDocumentParser,
            "Document families have separate parsers");

        string catalogPath = Path.Combine(Path.GetTempPath(), "rbq-catalog-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(catalogPath, """
                [{"id":"shared-law","title":"Shared legal source","kind":"legal_document",
                  "profile_ids":["ADM","GSC"],"licence_subcategories":["1.1.1","1.2"]}]
                """);
            DocumentSource source = SourceCatalog.Find(catalogPath, "shared-law");
            var catalogResult = chunker.CreateChunksWithReport([Page("Article 1. Une règle.")], new IngestionOptions
            {
                DocumentId = source.Id,
                Source = source,
                SourceType = SourceType.Reference,
                SkillIds = ["ADM:skill:1.1"]
            });
            check(catalogResult.Chunks[0].RelatedProfileIds.Count == 2 &&
                catalogResult.Chunks[0].LicenceSubcategories.Count == 2,
                "Catalog associations retained without duplicating source chunks");
            check(catalogResult.Chunks[0].ArticleNumber == "1", "Catalog selects the legal parser");
        }
        finally
        {
            File.Delete(catalogPath);
        }
    }
}
