using System.Text.Json.Serialization;

namespace Rbq.QueryApi;

/// <summary>
/// Embeddings + chat completions. The embedding model must stay in sync with the
/// ingestion pipeline, otherwise the stored vectors are not comparable.
/// </summary>
public sealed class OpenAiClient(HttpClient http)
{
    private readonly string _embeddingModel = DotEnv.Get("OPENAI_EMBEDDING_MODEL", "text-embedding-3-small");
    private readonly string _chatModel = DotEnv.Get("OPENAI_CHAT_MODEL", "gpt-4o-mini");

    public async Task<float[]> EmbedAsync(string input, CancellationToken ct)
    {
        var result = await http.PostAsync<EmbeddingResponse>(
            "embeddings", new { input, model = _embeddingModel }, ct);

        return result.Data.FirstOrDefault()?.Embedding
               ?? throw new HttpRequestException("OpenAI returned no embedding.");
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var result = await http.PostAsync<ChatResponse>("chat/completions", new
        {
            model = _chatModel,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        }, ct);

        return result.Choices.FirstOrDefault()?.Message.Content?.Trim()
               ?? throw new HttpRequestException("OpenAI returned no completion.");
    }

    private record EmbeddingResponse([property: JsonPropertyName("data")] List<EmbeddingItem> Data);
    private record EmbeddingItem([property: JsonPropertyName("embedding")] float[] Embedding);
    private record ChatResponse([property: JsonPropertyName("choices")] List<Choice> Choices);
    private record Choice([property: JsonPropertyName("message")] ChatMessage Message);
    private record ChatMessage([property: JsonPropertyName("content")] string? Content);
}
