using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rbq.Contracts;

namespace Rbq.Ingestion;

public static class ChunkFactory
{
    public const string ParserVersion = "rbq-structured-v2";

    public static Chunk Create(ExtractedDocument document, IngestionOptions options,
        IReadOnlyList<TextBlock> blocks, Competency? module = null,
        Competency? competency = null, Competency? skill = null,
        IReadOnlyList<string>? sectionPath = null, string? articleNumber = null)
    {
        string text = CombineBlockText(blocks);
        List<string> chunkContext = BuildChunkContext(document, options, module, competency, sectionPath);
        List<string> skillIds = GetAssociatedSkillIds(options, skill);
        List<string> potentialIssues = ListPotentialIssues(options, text, module, competency, skillIds);

        string chunkId = CreateChunkId(document, blocks, skillIds, text, sectionPath, articleNumber);

        return new Chunk
        {
            ChunkId = chunkId,
            DocumentId = document.Source.Id,
            DocumentVersion = document.Version,
            ProfileId = options.ProfileId,
            DocumentTitle = document.Source.Title,
            DocumentKind = JsonNamingPolicy.SnakeCaseLower.ConvertName(document.Source.Kind.ToString()),
            RelatedProfileIds = [.. document.Source.ProfileIds],
            LicenceSubcategories = [.. document.Source.LicenceSubcategories],
            SourceType = options.SourceType,
            SourceUrl = document.Source.Url,
            Filename = document.Filename,
            PageStart = blocks.Min(block => block.PageNumber),
            PageEnd = blocks.Max(block => block.PageNumber),
            Module = module,
            Competency = competency,
            Skill = skill,
            SkillIds = skillIds,
            ChunkContext = chunkContext,
            SectionPath = sectionPath?.ToList() ?? [],
            ArticleNumber = articleNumber,
            SourceBlockIds = blocks.Select(block => block.Id).ToList(),
            Content = text,
            ReviewIssues = potentialIssues,
            ParserVersion = ParserVersion
        };
    }

    private static string CreateChunkId(
        ExtractedDocument document,
        IReadOnlyList<TextBlock> blocks,
        IReadOnlyList<string> skillIds,
        string text,
        IReadOnlyList<string>? sectionPath,
        string? articleNumber)
    {
        string identity = JsonSerializer.Serialize(new
        {
            document.Source.Id,
            document.Version,
            ParserVersion,
            blockIds = blocks.Select(block => block.Id),
            skillIds,
            text,
            sectionPath,
            articleNumber
        });

        return Hash(identity);
    }

    private static string CombineBlockText(IReadOnlyList<TextBlock> blocks)
    {
        return string.Join("\n", blocks.Select(block => block.Text));
    }

    private static List<string> BuildChunkContext(
        ExtractedDocument document,
        IngestionOptions options,
        Competency? module,
        Competency? competency,
        IReadOnlyList<string>? sectionPath)
    {
        var chunkContext = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.ProfileId))
        {
            chunkContext.Add(options.ProfileId);
        }

        if (module is not null)
        {
            chunkContext.Add(module.Label);
        }

        if (competency is not null)
        {
            chunkContext.Add(competency.Label);
        }

        if (sectionPath is not null)
        {
            chunkContext.AddRange(sectionPath);
        }

        if (chunkContext.Count == 0)
        {
            chunkContext.Add(document.Source.Title);
        }

        return chunkContext;
    }

    private static List<string> GetAssociatedSkillIds(IngestionOptions options, Competency? skill)
    {
        if (skill is not null)
        {
            return new List<string> { skill.Id };
        }

        // Copy the configured IDs so the chunk has its own list.
        return new List<string>(options.SkillIds);
    }

    private static List<string> ListPotentialIssues(
        IngestionOptions options,
        string text,
        Competency? module,
        Competency? competency,
        IReadOnlyList<string> skillIds)
    {
        var potentialIssues = new List<string>();

        if (options.SourceType == SourceType.Profile && (module is null || competency is null))
        {
            potentialIssues.Add("missing_parent");
        }

        if (options.SourceType == SourceType.Reference && skillIds.Count == 0)
        {
            potentialIssues.Add("unmapped_reference");
        }

        if (text.Length > options.MaxCharacters)
        {
            potentialIssues.Add("oversized_unit");
        }

        return potentialIssues;
    }

    public static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
