using System.Net;
using System.Text;
using System.Text.Json;
using Rbq.Contracts;
using Rbq.Ingestion;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

DocumentPage Page(string text, int number = 1) => new()
{
    Text = text,
    Number = number,
    Filename = "profile.pdf"
};

var options = new IngestionOptions
{
    DocumentId = "adm-profile",
    ProfileId = "ADM",
    SourceType = SourceType.Profile
};
var chunker = new DocumentChunker();
Check(chunker.CreateChunks([], options).Count == 0, "Empty input");

var chunks = chunker.CreateChunks([
    Page("Module 1 – Finance\nÉléments de compétence abordés dans ce module :\n1. Résumé", 5),
    Page("Module 1 – Finance\nÉléments de compétence Habiletés requises\n1. Comprendre\nle bilan\n1.1. Expliquer\n• les actifs", 6),
    Page("Module 1 – Finance\nHabiletés requises\n• les passifs\n1.2.Calculer le solde.", 7)
], options);
Check(chunks.Count == 2, "Summary and repeated headers excluded");
Check(chunks[0].PageStart == 6 && chunks[0].PageEnd == 7, "Skill continues across pages");
Check(chunks[0].Competency!.Label == "Comprendre le bilan", "Multiline parent heading");
Check(chunks[0].Content.Contains("• les actifs\n• les passifs"), "Bullet preservation");
Check(chunks[1].Skill!.Id == "ADM:skill:1.2", "Number without following space");

List<Chunk> restored = ChunkJson.Deserialize(JsonSerializer.Serialize(chunks, ChunkJson.Options));
Check(restored[0].Skill!.Id == chunks[0].Skill!.Id && restored[0].EmbeddingText == chunks[0].EmbeddingText,
    "Shared object JSON round trip");
Check(JsonSerializer.Serialize(chunks, ChunkJson.Options).Contains("\"source_type\": \"profile\""),
    "Existing snake_case metadata format");

var changed = chunker.CreateChunks([Page("Module 1 – Finance\n1. Bilan\n1.1. Nouveau contenu")], options)[0];
Check(changed.Skill!.Id == chunks[0].Skill!.Id && changed.ChunkId != chunks[0].ChunkId,
    "Stable skill identity with versioned source identity");
var duplicate = chunker.CreateChunks([Page("Module 1 – Finance\n1. Bilan\n1.1. A\n1.1. B")], options);
Check(duplicate.All(chunk => chunk.ReviewIssues.Contains("duplicate_skill")), "All duplicate occurrences flagged");
var orphan = chunker.CreateChunks([Page("Module 1 – Finance\n1.1. Sans parent")], options);
Check(orphan[0].ReviewIssues.Contains("missing_parent"), "Missing parent flagged");

var referenceOptions = new IngestionOptions
{
    DocumentId = "accounting-reference",
    SourceType = SourceType.Reference,
    SkillIds = ["ADM:skill:1.2"],
    MaxCharacters = 100
};
var references = chunker.CreateChunks([Page(new string('A', 80) + "\n\n" + new string('B', 120))], referenceOptions);
Check(references.Count == 2 && references[1].ReviewIssues.Contains("oversized_unit"), "Long reference kept intact and flagged");
Check(references[0].ProfileId is null, "Reference does not require a single profile");

// No real network calls: inspect requests and emulate provider responses.
var embeddingHandler = new FakeHandler(request =>
{
    string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
    Check(body.Contains("text-embedding-3-small"), "Embedding model matches database");
    return JsonSerializer.Serialize(new
    {
        data = new[]
        {
            new { index = 1, embedding = Enumerable.Repeat(2f, 1536) },
            new { index = 0, embedding = Enumerable.Repeat(1f, 1536) }
        }
    });
});
using var embeddingHttp = new HttpClient(embeddingHandler) { BaseAddress = new Uri("https://example.test/") };
var vectors = await new EmbeddingClient(embeddingHttp).EmbedAsync(chunks);
Check(vectors[0][0] == 1 && vectors[1][0] == 2, "Embedding response order restored");

var uploadHandler = new FakeHandler(request =>
{
    var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
    Check(body.RootElement[0].GetProperty("metadata").GetProperty("skill").GetProperty("id").GetString() == "ADM:skill:1.1",
        "Upload retains typed metadata as JSON");
    Check(request.RequestUri!.Query.Contains("on_conflict=chunk_id"), "Idempotent upsert key");
    return "[]";
});
using var uploadHttp = new HttpClient(uploadHandler) { BaseAddress = new Uri("https://example.test/") };
var uploader = new SupabaseUploader(uploadHttp, new EmbeddingClient(embeddingHttp));
Check(await uploader.UploadAsync(chunks) == 2, "Upload count");
bool rejected = false;
try { await uploader.UploadAsync(orphan); }
catch (InvalidDataException) { rejected = true; }
Check(rejected, "Flagged data rejected before network calls");

int readRequests = 0;
var referenceHandler = new FakeHandler(request =>
{
    readRequests++;
    if (readRequests > 1)
        return "[]";
    Check(Uri.UnescapeDataString(request.RequestUri!.Query).Contains("document_version"), "Evidence scoped to document version");
    return JsonSerializer.Serialize(new[] { new { metadata = references[0] } }, ChunkJson.Options);
});
using var referenceHttp = new HttpClient(referenceHandler) { BaseAddress = new Uri("https://example.test/") };
var evidence = await new ReferenceChunkReader(referenceHttp).ReadAsync(
    "ADM:skill:1.2", references[0].DocumentId, references[0].DocumentVersion);
Check(evidence.Count == 1 && readRequests == 2, "Typed reference reading and pagination");

ArchitectureChecks.Run(Check);

if (args.Length > 0)
{
    var pythonChunks = ChunkJson.Deserialize(await File.ReadAllTextAsync(args[0]));
    Check(pythonChunks.Count == 174 && pythonChunks.All(chunk => chunk.Skill is not null), "Reads 174 existing Python chunks");
}
if (args.Length > 1)
{
    var pdfChunks = chunker.CreateChunks(new PdfTextExtractor().Extract(args[1]), options);
    Check(pdfChunks.Count == 174, "Official Administration PDF: 174 skills");
    Check(pdfChunks.Select(chunk => chunk.Module!.Id).Distinct().Count() == 4, "Official PDF: four modules");
    Check(pdfChunks.Select(chunk => chunk.Competency!.Id).Distinct().Count() == 24, "Official PDF: 24 competency elements");
    Check(pdfChunks.All(chunk => chunk.ReviewIssues.Count == 0), "Official PDF: no automatic review flags");
}
for (int index = 2; index < args.Length; index++)
{
    string profile = index == 2 ? "GSC" : "ETC-1.4";
    int expected = index == 2 ? 130 : 205;
    var pages = new PdfTextExtractor().Extract(args[index]);
    var result = chunker.Parse(pages, new IngestionOptions
    {
        DocumentId = profile,
        ProfileId = profile,
        SourceType = SourceType.Profile
    });
    Check(result.Chunks.Count == expected, $"{profile}: expected skill count");
    Check(result.Chunks.All(chunk => chunk.ReviewIssues.Count == 0), $"{profile}: no review flags");
    Check(result.UnclassifiedBlocks.Count == 0, $"{profile}: no unclassified blocks");
    int accounted = result.Chunks.Sum(chunk => chunk.SourceBlockIds.Count) + result.ExcludedBlocks.Count;
    Check(accounted == pages.Sum(page => page.Blocks.Count), $"{profile}: all extracted blocks accounted for");
}
Console.WriteLine($"{passed} checks passed.");

sealed class FakeHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(respond(request), Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
