using System.Net.Http.Json;
using Rbq.Contracts;

namespace Rbq.Ingestion;

public sealed class SupabaseUploader(HttpClient http, EmbeddingClient embeddings)
{
    public async Task<int> UploadAsync(IReadOnlyList<Chunk> chunks, CancellationToken ct = default)
    {
        // Validate everything before spending money on embedding requests.
        foreach (Chunk chunk in chunks)
            ChunkValidation.RequireReadyForUpload(chunk);

        List<float[]> vectors = await embeddings.EmbedAsync(chunks, ct);
        for (int offset = 0; offset < chunks.Count; offset += 64)
        {
            var rows = new List<object>();
            for (int index = offset; index < Math.Min(offset + 64, chunks.Count); index++)
            {
                Chunk chunk = chunks[index];
                rows.Add(new
                {
                    chunk_id = chunk.ChunkId,
                    content = chunk.EmbeddingText,
                    filename = chunk.Filename,
                    page_number = chunk.PageStart,
                    embedding = vectors[index],
                    metadata = chunk
                });
            }

            using var request = new HttpRequestMessage(HttpMethod.Post,
                "rest/v1/document_chunks?on_conflict=chunk_id");
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
            request.Content = JsonContent.Create(rows, options: ChunkJson.Options);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        return chunks.Count;
    }
}
