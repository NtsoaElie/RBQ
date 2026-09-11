using System.Text.Json.Serialization;

namespace Rbq.QueryApi;

/// <summary>
/// Vector search against the `document_chunks` table the ingestion pipeline fills,
/// through the `match_documents` RPC (see sql/match_documents.sql).
/// </summary>
public sealed class SupabaseClient(HttpClient http)
{
    public async Task<IReadOnlyList<ChunkMatch>> MatchChunksAsync(
        float[] queryEmbedding, int matchCount, CancellationToken ct)
    {
        var rows = await http.PostAsync<List<MatchRow>>(
            "rest/v1/rpc/match_documents",
            new { query_embedding = queryEmbedding, match_count = matchCount },
            ct);

        return rows
            .Select(row => new ChunkMatch(
                row.Content,
                Path.GetFileName(row.Filename) ?? row.Filename,
                row.PageNumber,
                row.Similarity))
            .ToList();
    }

    private record MatchRow(
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("similarity")] double Similarity);
}
