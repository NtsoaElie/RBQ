using System.Net.Http.Headers;
using System.Text.Json;
using Rbq.Contracts;
using Rbq.Ingestion;

// Ingestion runs without credentials. Only the upload command needs API keys.
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("ingest FILE --document-id ID --source-type profile|reference|exam_info --output FILE [--kind profile|legal|exam_info|technical] [--title TITLE] [--profile-id ADM] [--source-url URL] [--skill-id ID ...]");
    Console.WriteLine("Optional: --catalog SOURCES.json supplies title, kind, URL and exam associations for --document-id.");
    Console.WriteLine("upload CHUNKS.json");
    return 0;
}

try
{
    if (args.Length < 2)
        throw new ArgumentException("Provide an input file.");

    if (args[0] == "ingest")
    {
        var values = new Dictionary<string, string>();
        var skillIds = new List<string>();
        string[] allowed = ["--document-id", "--source-type", "--output", "--profile-id", "--source-url", "--skill-id", "--kind", "--title", "--catalog"];
        for (int index = 2; index < args.Length; index += 2)
        {
            string name = args[index];
            if (!allowed.Contains(name) || index + 1 >= args.Length)
                throw new ArgumentException($"Unknown option or missing value: {name}");
            if (name == "--skill-id")
                skillIds.Add(args[index + 1]);
            else if (!values.TryAdd(name, args[index + 1]))
                throw new ArgumentException($"Duplicate option: {name}");
        }

        string Require(string name) => values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Required option: {name}");

        DocumentSource? source = values.TryGetValue("--catalog", out string? catalogPath)
            ? SourceCatalog.Find(catalogPath, Require("--document-id")) : null;
        string sourceTypeName = values.GetValueOrDefault("--source-type") ?? (source?.Kind switch
        {
            DocumentKind.CompetencyProfile => "profile",
            DocumentKind.ExamInformation => "exam_info",
            DocumentKind.LegalDocument or DocumentKind.TechnicalDocument => "reference",
            _ => throw new ArgumentException("Provide --source-type or --catalog.")
        });

        SourceType sourceType = sourceTypeName switch
        {
            "profile" => SourceType.Profile,
            "reference" => SourceType.Reference,
            "exam_info" => SourceType.ExamInfo,
            _ => throw new ArgumentException("Unknown source type.")
        };

        var options = new IngestionOptions
        {
            Source = source,
            Title = values.GetValueOrDefault("--title"),
            Kind = values.GetValueOrDefault("--kind") switch
            {
                null => null,
                "profile" => DocumentKind.CompetencyProfile,
                "legal" => DocumentKind.LegalDocument,
                "exam_info" => DocumentKind.ExamInformation,
                "technical" => DocumentKind.TechnicalDocument,
                _ => throw new ArgumentException("Unknown document kind.")
            },
            DocumentId = Require("--document-id"),
            ProfileId = values.GetValueOrDefault("--profile-id") ??
                (sourceType == SourceType.Profile && source?.ProfileIds.Count == 1 ? source.ProfileIds[0] : null),
            SourceType = sourceType,
            SourceUrl = values.GetValueOrDefault("--source-url"),
            SkillIds = skillIds
        };

        var chunkingService = new DocumentChunkingService();
        ParsingResult result = await chunkingService.CreateChunksFromDocumentAsync(args[1], options);
        List<Chunk> chunks = result.Chunks;

        string output = Path.GetFullPath(Require("--output"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(chunks, ChunkJson.Options));
        string reportPath = output + ".report.json";
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            result.ParserVersion,
            ChunkCount = chunks.Count,
            result.Warnings,
            result.UnclassifiedBlocks,
            result.ExcludedBlocks
        }, ChunkJson.Options));
        Console.WriteLine($"{chunks.Count} chunks; {chunks.Count(chunk => chunk.ReviewIssues.Count > 0)} require review. Saved {output}");
        Console.WriteLine($"Review report: {reportPath}");
        foreach (string warning in result.Warnings) Console.WriteLine("Warning: " + warning);
        return chunks.Count == 0 || result.UnclassifiedBlocks.Count > 0 ? 2 : 0;
    }

    if (args[0] == "upload" && args.Length == 2)
    {
        List<Chunk> chunks = ChunkJson.Deserialize(await File.ReadAllTextAsync(args[1]));
        using var openAiHttp = new HttpClient();
        openAiHttp.BaseAddress = new Uri("https://api.openai.com/v1/");
        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPEN_API_KEY");
        if (string.IsNullOrWhiteSpace(openAiKey))
            throw new ArgumentException("Set OPENAI_API_KEY in your environment.");
        openAiHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);

        string RequireEnvironment(string name) => Environment.GetEnvironmentVariable(name)
            ?? throw new ArgumentException($"Set {name} in your environment.");

        using var supabaseHttp = new HttpClient();
        supabaseHttp.BaseAddress = new Uri(RequireEnvironment("SUPABASE_URL").TrimEnd('/') + "/");
        string supabaseKey = RequireEnvironment("SUPABASE_PRIVATE_KEY");
        supabaseHttp.DefaultRequestHeaders.Add("apikey", supabaseKey);
        supabaseHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

        var embeddings = new EmbeddingClient(openAiHttp);
        var uploader = new SupabaseUploader(supabaseHttp, embeddings);
        Console.WriteLine($"Uploaded {await uploader.UploadAsync(chunks)} chunks.");
        return 0;
    }

    throw new ArgumentException("Unknown command. Use --help.");
}
catch (Exception error) when (error is ArgumentException or IOException or HttpRequestException or JsonException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
