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
        string text = string.Join("\n", blocks.Select(block => block.Text));
        var breadcrumb = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.ProfileId)) breadcrumb.Add(options.ProfileId);
        if (module is not null) breadcrumb.Add(module.Label);
        if (competency is not null) breadcrumb.Add(competency.Label);
        if (sectionPath is not null) breadcrumb.AddRange(sectionPath);
        if (breadcrumb.Count == 0) breadcrumb.Add(document.Source.Title);

        List<string> skillIds = skill is null ? [.. options.SkillIds] : [skill.Id];
        var issues = new List<string>();
        if (options.SourceType == SourceType.Profile && (module is null || competency is null))
            issues.Add("missing_parent");
        if (options.SourceType == SourceType.Reference && skillIds.Count == 0)
            issues.Add("unmapped_reference");
        if (text.Length > options.MaxCharacters) issues.Add("oversized_unit");

        string identity = JsonSerializer.Serialize(new
        {
            document.Source.Id, document.Version, ParserVersion,
            blockIds = blocks.Select(block => block.Id), skillIds, text, sectionPath, articleNumber
        });

        return new Chunk
        {
            ChunkId = Hash(identity),
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
            Breadcrumb = breadcrumb,
            SectionPath = sectionPath?.ToList() ?? [],
            ArticleNumber = articleNumber,
            SourceBlockIds = blocks.Select(block => block.Id).ToList(),
            Content = text,
            ReviewIssues = issues,
            ParserVersion = ParserVersion
        };
    }

    public static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
