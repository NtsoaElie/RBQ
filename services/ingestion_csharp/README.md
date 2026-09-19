# C# ingestion

Requires the .NET 8 SDK. Run commands from the repository root.

The shared library at `services/shared/Rbq.Contracts` contains ordinary C# classes:

- `Competency`: an ID, original code and label.
- `Chunk`: text, source/version, page range, hierarchy and linked skill IDs.
- `SourceType`: Profile, Reference or ExamInfo.
- `ChunkJson`: snake_case serialization compatible with the existing Python export.

Both the ingestion project and query API reference that library. Future quiz
generation should accept these `Chunk` objects rather than invent another source
model. A question will still need its own model for options, answer, explanation,
primary assessed skill and supporting chunk IDs; that generator is not implemented.

## Extract and chunk a competency profile

```powershell
dotnet run --project services/ingestion_csharp -- ingest ADM_ProfilDeCompetences.pdf --document-id rbq-administration --profile-id ADM --source-type profile --output output/administration-csharp.json
```

No credentials are needed. `PdfTextExtractor` reads physical PDF pages, then
`DocumentChunker` follows module/competency/skill headings. The constructor assigns
one property per line. Extracted skill IDs retain the `ADM:skill:1.2` convention.

For a focused reference excerpt, supply its reviewed skill associations:

```powershell
dotnet run --project services/ingestion_csharp -- ingest bilan.txt --document-id reference-bilan --source-type reference --skill-id ADM:skill:1.2 --output output/bilan.json
```

Reference sources do not require a single profile ID. Repeated `--skill-id` options
apply to every resulting passage: do not map an entire textbook this way.
Unmapped references are exported with review flags and cannot be uploaded yet.
`--source-type exam_info` keeps exam instructions distinguishable from answer
evidence. PDF and UTF-8 TXT are supported; HTML pages must be converted explicitly.
Use `--source-url` to retain the original document URL.

## Embed and upload

Apply the existing additive migration `services/ingestion_csharp/schema.sql`
first. Set process environment variables `OPENAI_API_KEY`, `SUPABASE_URL`, and
`SUPABASE_PRIVATE_KEY`. The old `OPEN_API_KEY` is a fallback. This CLI does not
automatically load a `.env` file.

```powershell
dotnet run --project services/ingestion_csharp -- upload output/administration-csharp.json
```

Upload validates all chunks before requesting embeddings. `EmbeddingClient` sends
up to 64 contextual texts per request to `text-embedding-3-small`, checks vector
dimensions and ordering, then `SupabaseUploader` upserts content, vectors and typed
metadata into the existing table. API errors stop the command with a nonzero exit.
Partial uploads can be retried: the chunk ID is the upsert key.

The C# parser has its own parser/version-based chunk IDs; it does not overwrite
Python-ingested revisions automatically. Retire or select older revisions
explicitly. The existing Q&A similarity RPC still searches all stored revisions.

## Reading the same objects for future quizzes

`SupabaseClient` in the query API now exposes:

```csharp
List<Chunk> references = await supabase.GetReferenceChunksAsync(
    skillId,
    documentId,
    documentVersion,
    cancellationToken);
```

The shared `ReferenceChunkReader` requests reference passages mapped to that skill,
pages through results, deserializes metadata as `Chunk`, validates it, and checks
the requested document version. An empty list means there is no mapped evidence;
the future quiz generator should abstain instead of using the profile as proof.
Existing `/api/ask` behavior is unchanged. No quiz HTTP endpoint is added here.

## Tests

These are executable checks without a test-framework dependency:

```powershell
dotnet run --project tests/Rbq.Ingestion.Tests
```

Optional real-source checks (provide your own downloaded files):

```powershell
dotnet run --project tests/Rbq.Ingestion.Tests -- output/administration.json ADM_ProfilDeCompetences.pdf
```

The optional checks validate the 174-skill Administration sample previously tested
in this repository. These counts are a regression baseline, not a promise about
future RBQ revisions. Tests include JSON compatibility, page continuations,
headings, bullets, duplicate flags, mocked embeddings/uploads and paginated typed
reference retrieval. No credentials are used by tests.

## Limits of this first C# implementation

- The RBQ website catalog/crawler and automatic reference-to-skill mapping are not
  implemented. The CLI reads files you have downloaded.
- Profile recognition assumes numbered modules and skills. Other layouts require
  validation. Text before a recognized module can be omitted, and numeric-only
  lines are treated as page numbers. Wrapped skill labels retain their first line,
  while full continuation text remains in `Content`.
- Reference splitting is paragraph-based, not article-aware legal parsing. Long
  units are preserved and flagged against a 2,000-character budget; no exact token
  budget, OCR, table reconstruction or automatic legal currency checks are provided.
- Manual reference mapping and empty review flags do not establish factual
  correctness. Review supporting passages before using them to generate questions.
- Live embedding and Supabase upload require credentials and were not executed.

PDF extraction uses [PdfPig's content-order text extractor](https://github.com/UglyToad/PdfPig).
Python source code and tooling have been removed; the runtime is C# only.
