namespace Rbq.QueryApi;

/// <summary>What the client sends to POST /api/ask.</summary>
public record AskRequest(string Question, int? MatchCount);

/// <summary>One retrieved chunk, as shown to the user.</summary>
public record Source(string Content, string Filename, int PageNumber, double Similarity);

/// <summary>What POST /api/ask returns.</summary>
public record AskResponse(string Answer, IReadOnlyList<Source> Sources);

/// <summary>A row returned by the Supabase `match_documents` RPC.</summary>
public record ChunkMatch(string Content, string Filename, int PageNumber, double Similarity);
