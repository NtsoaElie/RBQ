using System.Text;

namespace Rbq.QueryApi;

/// <summary>The query pipeline: embed the question, retrieve chunks, answer from them.</summary>
public sealed class RagService(OpenAiClient openAi, SupabaseClient supabase)
{
    private const int DefaultMatchCount = 5;

    private const string SystemPrompt = """
        You are the RBQPrep Coach IA, helping candidates prepare the Regie du batiment
        du Quebec licensing exams. Answer only from the excerpts provided.
        Cite the source of each claim as (filename, page N).
        If the excerpts do not contain the answer, say so plainly instead of guessing.
        Answer in the language the question was asked in; default to Quebec French.
        """;

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct)
    {
        var question = request.Question.Trim();
        var matchCount = Math.Clamp(request.MatchCount ?? DefaultMatchCount, 1, 20);

        var embedding = await openAi.EmbedAsync(question, ct);
        var matches = await supabase.MatchChunksAsync(embedding, matchCount, ct);

        if (matches.Count == 0)
            return new AskResponse("No relevant passage was found in the indexed documents.", []);

        var answer = await openAi.ChatAsync(SystemPrompt, BuildPrompt(question, matches), ct);
        var sources = matches
            .Select(m => new Source(m.Content, m.Filename, m.PageNumber, m.Similarity))
            .ToList();

        return new AskResponse(answer, sources);
    }

    private static string BuildPrompt(string question, IReadOnlyList<ChunkMatch> matches)
    {
        var prompt = new StringBuilder("Excerpts:\n\n");

        foreach (var match in matches)
            prompt.AppendLine($"[{match.Filename}, page {match.PageNumber}]\n{match.Content}\n");

        return prompt.Append("Question: ").Append(question).ToString();
    }
}
