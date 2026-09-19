using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rbq.Contracts;

namespace Rbq.Ingestion;

public sealed class DocumentChunker
{
    private static readonly Regex ModuleHeading = new(
        @"^Module\s+(\d+)\s*[–—:-]\s*(.+)", RegexOptions.IgnoreCase);
    private static readonly Regex NumberedHeading = new(
        @"^(\d+(?:\.\d+)*)(?:\.\s*|\s+)(\S.*)");

    public List<Chunk> CreateChunks(IReadOnlyList<DocumentPage> pages, IngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DocumentId))
            throw new ArgumentException("DocumentId is required.");
        if (options.SourceType == SourceType.Profile && string.IsNullOrWhiteSpace(options.ProfileId))
            throw new ArgumentException("A competency profile needs a ProfileId.");
        if (!Enum.IsDefined(options.SourceType) || options.MaxCharacters < 100)
            throw new ArgumentException("Invalid source type or character budget.");
        if (pages.Count == 0)
            return [];
        if (pages.Select(page => page.Filename).Distinct().Count() != 1)
            throw new ArgumentException("Process one document at a time.");
        if (pages.Any(page => page.Number < 1) ||
            !pages.Select(page => page.Number).SequenceEqual(pages.Select(page => page.Number).Order()))
            throw new ArgumentException("Pages must be in ascending order with positive numbers.");

        string version = Hash(string.Join("\n", pages.Select(page => page.Text)));
        if (options.SourceType == SourceType.Profile)
            return ParseProfile(pages, options, version);

        return SplitReference(pages, options, version);
    }

    private static List<Chunk> ParseProfile(
        IReadOnlyList<DocumentPage> pages, IngestionOptions options, string version)
    {
        var chunks = new List<Chunk>();
        Competency? module = null;
        Competency? competency = null;
        Competency? skill = null;
        var lines = new List<string>();
        int startPage = 0;
        int endPage = 0;
        bool inSummary = false;

        // Save the current skill before changing its parent or starting another skill.
        void SaveCurrentSkill()
        {
            if (skill is null)
                return;

            chunks.Add(BuildChunk(
                string.Join("\n", lines), startPage, endPage, pages[0].Filename,
                options, version, module, competency, skill));
        }

        foreach (DocumentPage page in pages)
        {
            foreach (string rawLine in page.Text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.All(char.IsDigit))
                    continue;
                if (line == "Administration" || line == "Gestion de projets et de chantiers" ||
                    string.Equals(line, options.ProfileId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.Contains("Éléments de compétence") || line.Contains("Habiletés requises"))
                {
                    inSummary = line.Contains("abordés");
                    continue;
                }

                Match moduleMatch = ModuleHeading.Match(line);
                if (moduleMatch.Success)
                {
                    string code = moduleMatch.Groups[1].Value;
                    if (module?.Code == code)
                        continue; // Repeated header on a continuation page.

                    SaveCurrentSkill();
                    module = new Competency
                    {
                        Id = $"{options.ProfileId}:module:{code}",
                        Code = code,
                        Label = moduleMatch.Groups[2].Value
                    };
                    competency = null;
                    skill = null;
                    lines.Clear();
                    inSummary = false;
                    continue;
                }

                if (inSummary)
                    continue;

                Match heading = NumberedHeading.Match(line);
                if (heading.Success && module is not null)
                {
                    SaveCurrentSkill();
                    string code = heading.Groups[1].Value;
                    string label = heading.Groups[2].Value;

                    if (!code.Contains('.'))
                    {
                        competency = new Competency
                        {
                            Id = $"{module.Id}:competency:{code}",
                            Code = code,
                            Label = label
                        };
                        skill = null;
                        lines.Clear();
                    }
                    else
                    {
                        if (competency is not null && !code.StartsWith(competency.Code + "."))
                            competency = null;

                        skill = new Competency
                        {
                            Id = $"{options.ProfileId}:skill:{code}",
                            Code = code,
                            Label = label
                        };
                        lines = [line];
                        startPage = page.Number;
                        endPage = page.Number;
                    }
                    continue;
                }

                if (skill is not null)
                {
                    lines.Add(line);
                    endPage = page.Number;
                }
                else if (competency is not null)
                {
                    competency = new Competency
                    {
                        Id = competency.Id,
                        Code = competency.Code,
                        Label = competency.Label + " " + line
                    };
                }
            }
        }

        SaveCurrentSkill();
        // Flag every occurrence so the first duplicate cannot slip into an upload.
        foreach (var group in chunks.GroupBy(chunk => chunk.Skill!.Id))
        {
            if (group.Count() > 1)
                foreach (Chunk chunk in group)
                    chunk.ReviewIssues.Add("duplicate_skill");
        }
        return chunks;
    }

    private static List<Chunk> SplitReference(
        IReadOnlyList<DocumentPage> pages, IngestionOptions options, string version)
    {
        var chunks = new List<Chunk>();
        var paragraphs = new List<string>();
        int startPage = 0;
        int endPage = 0;

        void SaveParagraphs()
        {
            if (paragraphs.Count == 0)
                return;
            chunks.Add(BuildChunk(string.Join("\n\n", paragraphs), startPage, endPage,
                pages[0].Filename, options, version));
            paragraphs.Clear();
        }

        foreach (DocumentPage page in pages)
        {
            foreach (string rawParagraph in Regex.Split(page.Text, @"\n\s*\n"))
            {
                string paragraph = rawParagraph.Trim();
                if (paragraph.Length == 0)
                    continue;

                int combinedLength = paragraphs.Sum(part => part.Length) + paragraphs.Count * 2 + paragraph.Length;
                if (combinedLength > options.MaxCharacters)
                    SaveParagraphs();
                if (paragraphs.Count == 0)
                    startPage = page.Number;

                paragraphs.Add(paragraph);
                endPage = page.Number;
            }
        }
        SaveParagraphs();
        return chunks;
    }

    private static Chunk BuildChunk(
        string text, int startPage, int endPage, string filename,
        IngestionOptions options, string version,
        Competency? module = null, Competency? competency = null, Competency? skill = null)
    {
        var breadcrumb = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.ProfileId))
            breadcrumb.Add(options.ProfileId);
        if (module is not null)
            breadcrumb.Add(module.Label);
        if (competency is not null)
            breadcrumb.Add(competency.Label);

        List<string> skillIds = skill is null ? [.. options.SkillIds] : [skill.Id];
        var reviewIssues = new List<string>();
        if (options.SourceType == SourceType.Profile && (module is null || competency is null))
            reviewIssues.Add("missing_parent");
        if (options.SourceType == SourceType.Reference && skillIds.Count == 0)
            reviewIssues.Add("unmapped_reference");
        if (text.Length > options.MaxCharacters)
            reviewIssues.Add("oversized_unit");

        // Structured identity avoids ambiguity between separators in source text.
        string identity = JsonSerializer.Serialize(new
        {
            options.DocumentId, version, startPage, endPage, skillIds, text,
            parserVersion = "rbq-csharp-v1"
        });

        return new Chunk
        {
            ChunkId = Hash(identity),
            DocumentId = options.DocumentId,
            DocumentVersion = version,
            ProfileId = options.ProfileId,
            SourceType = options.SourceType,
            SourceUrl = options.SourceUrl,
            Filename = filename,
            PageStart = startPage,
            PageEnd = endPage,
            Module = module,
            Competency = competency,
            Skill = skill,
            SkillIds = skillIds,
            Breadcrumb = breadcrumb,
            Content = text,
            ReviewIssues = reviewIssues
        };
    }

    private static string Hash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
