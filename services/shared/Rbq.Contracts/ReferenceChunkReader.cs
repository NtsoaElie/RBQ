using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Rbq.Contracts;

/// <summary>Reads source evidence for one skill and an explicitly selected document revision.</summary>
public sealed class ReferenceChunkReader(HttpClient http)
{
    public async Task<List<Chunk>> ReadAsync(
        string skillId, string documentId, string documentVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(skillId) || string.IsNullOrWhiteSpace(documentId) ||
            string.IsNullOrWhiteSpace(documentVersion))
            throw new ArgumentException("Skill ID, document ID and version are required.");

        // JSON containment avoids assigning every source under an exam to every skill.
        string filter = System.Text.Json.JsonSerializer.Serialize(new
        {
            source_type = "reference",
            skill_ids = new[] { skillId },
            document_id = documentId,
            document_version = documentVersion
        });
        string query = "rest/v1/document_chunks?select=metadata&metadata=cs." + Uri.EscapeDataString(filter);
        query += "&order=chunk_id.asc";

        var chunks = new List<Chunk>();
        int offset = 0;
        while (true)
        {
            var rows = await http.GetFromJsonAsync<List<MetadataRow>>(
                query + $"&limit=100&offset={offset}", ChunkJson.Options, ct)
                ?? throw new InvalidDataException("Missing source rows.");
            if (rows.Count == 0)
                break;

            foreach (MetadataRow row in rows)
            {
                Chunk chunk = row.Metadata;
                // Validate again locally; incomplete or mismatched evidence must not reach a quiz.
                ChunkValidation.RequireReadyForUpload(chunk);
                if (chunk.SourceType != SourceType.Reference || !chunk.SkillIds.Contains(skillId) ||
                    chunk.DocumentId != documentId || chunk.DocumentVersion != documentVersion)
                    throw new InvalidDataException("Reference metadata does not match the requested scope.");
                chunks.Add(chunk);
            }
            offset += rows.Count;
        }
        return chunks;
    }

    private sealed class MetadataRow
    {
        [JsonPropertyName("metadata")]
        public required Chunk Metadata { get; init; }
    }
}
