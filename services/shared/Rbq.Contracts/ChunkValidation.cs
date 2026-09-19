namespace Rbq.Contracts;

public static class ChunkValidation
{
    public static void RequireReadyForUpload(Chunk chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.ChunkId) ||
            string.IsNullOrWhiteSpace(chunk.DocumentId) ||
            string.IsNullOrWhiteSpace(chunk.DocumentVersion) ||
            string.IsNullOrWhiteSpace(chunk.Filename) ||
            string.IsNullOrWhiteSpace(chunk.Content))
            throw new InvalidDataException("Chunk identity, source and content are required.");
        if (chunk.PageStart < 1 || chunk.PageEnd < chunk.PageStart)
            throw new InvalidDataException("Invalid chunk page range.");
        if (!Enum.IsDefined(chunk.SourceType) || chunk.ReviewIssues.Count > 0)
            throw new InvalidDataException("Resolve chunk review issues before uploading.");
        if (chunk.SourceType == SourceType.Profile &&
            (chunk.Module is null || chunk.Competency is null || chunk.Skill is null ||
             string.IsNullOrWhiteSpace(chunk.ProfileId)))
            throw new InvalidDataException("A profile skill requires its complete hierarchy.");
        if (chunk.SourceType == SourceType.Reference && chunk.SkillIds.Count == 0)
            throw new InvalidDataException("Reference passages need explicit skill mappings.");
        if (chunk.SkillIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Skill IDs cannot be blank.");
    }
}
