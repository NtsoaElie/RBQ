using System.Net.Http.Json;
using Rbq.Contracts;

namespace Rbq.Ingestion;

public sealed class EmbeddingClient(HttpClient http)
{
    public async Task<List<float[]>> EmbedAsync(IReadOnlyList<Chunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Any(chunk => string.IsNullOrWhiteSpace(chunk.Content)))
            throw new ArgumentException("Cannot embed empty chunks.");

        var vectors = new List<float[]>();
        foreach (Chunk[] batch in chunks.Chunk(64))
        {
            var request = new
            {
                model = "text-embedding-3-small",
                input = batch.Select(chunk => chunk.EmbeddingText).ToArray()
            };

            using var response = await http.PostAsJsonAsync("embeddings", request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
                ?? throw new InvalidDataException("No embedding response.");

            var ordered = result.Data.OrderBy(item => item.Index).ToList();
            if (!ordered.Select(item => item.Index).SequenceEqual(Enumerable.Range(0, batch.Length)))
                throw new InvalidDataException("Missing or duplicate embedding indices.");
            if (ordered.Any(item => item.Embedding.Length != 1536 || item.Embedding.Any(v => !float.IsFinite(v))))
                throw new InvalidDataException("Expected finite vectors with 1536 dimensions.");

            vectors.AddRange(ordered.Select(item => item.Embedding));
        }
        return vectors;
    }

    private sealed class EmbeddingResponse
    {
        public required List<EmbeddingItem> Data { get; init; }
    }

    private sealed class EmbeddingItem
    {
        public required int Index { get; init; }
        public required float[] Embedding { get; init; }
    }
}
