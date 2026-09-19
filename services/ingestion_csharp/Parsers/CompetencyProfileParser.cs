using System.Text.RegularExpressions;
using Rbq.Contracts;

namespace Rbq.Ingestion.Parsers;

public sealed class CompetencyProfileParser : IDocumentParser
{
    private static readonly Regex ModuleHeading = new(@"^Module\s+(\d+)\s*[–—:-]\s*(.+)", RegexOptions.IgnoreCase);
    // Atomic numbering prevents a citation like "3.001," becoming competency "3".
    // A missing final dot is accepted only before an uppercase heading, not "2.1 art.".
    private static readonly Regex NumberedHeading = new(@"^((?>\d+(?:\.\d+)*))(?:\.\s*(\p{L}.*)|\s+(\p{Lu}.*))");

    public ParsingResult Parse(ExtractedDocument document, IngestionOptions options)
    {
        var result = new ParsingResult();
        Competency? module = null;
        Competency? competency = null;
        Competency? skill = null;
        var skillBlocks = new List<TextBlock>();
        bool inSummary = false;
        bool collectingModuleTitle = false;
        bool collectingSkillLabel = false;
        var tocPages = document.Blocks
            .Where(block => block.Text.StartsWith("Table des matières", StringComparison.OrdinalIgnoreCase))
            .Select(block => block.PageNumber).ToHashSet();
        int firstBodyPage = document.Blocks
            .Where(block => !tocPages.Contains(block.PageNumber) && ModuleHeading.IsMatch(block.Text))
            .Select(block => block.PageNumber).DefaultIfEmpty(int.MaxValue).Min();

        void SaveSkill()
        {
            if (skill is null) return;
            result.Chunks.Add(ChunkFactory.Create(document, options, skillBlocks, module, competency, skill));
            skill = null;
            skillBlocks = [];
        }

        foreach (TextBlock block in document.Blocks)
        {
            string line = block.Text;
            if (tocPages.Contains(block.PageNumber))
            {
                result.Exclude(block, "table_of_contents");
                continue;
            }

            Match moduleMatch = ModuleHeading.Match(line);
            if (moduleMatch.Success)
            {
                string code = moduleMatch.Groups[1].Value;
                if (module?.Code != code)
                {
                    SaveSkill();
                    competency = null;
                }
                module = new Competency
                {
                    Id = $"{options.ProfileId}:module:{code}",
                    Code = code,
                    Label = moduleMatch.Groups[2].Value
                };
                collectingModuleTitle = true;
                inSummary = false;
                result.Exclude(block, "module_heading_context");
                continue;
            }

            bool competencyHeader = line.Contains("Éléments de compétence", StringComparison.OrdinalIgnoreCase);
            bool skillHeader = Regex.IsMatch(line, @"Habiletés\s+(minimalement\s+)?requises", RegexOptions.IgnoreCase);
            if (competencyHeader || skillHeader)
            {
                collectingModuleTitle = false;
                inSummary = line.Contains("abordés", StringComparison.OrdinalIgnoreCase);
                if (inSummary) SaveSkill();
                result.Exclude(block, inSummary ? "module_summary_heading" : "table_column_heading");
                continue;
            }
            if (module is null)
            {
                // Retain source evidence in the audit even when it precedes the profile body.
                if (firstBodyPage != int.MaxValue && block.PageNumber < firstBodyPage)
                    result.Exclude(block, "profile_front_matter");
                else if (NumberedHeading.IsMatch(line)) result.UnclassifiedBlocks.Add(block);
                else result.Exclude(block, "profile_front_matter");
                continue;
            }
            if (inSummary)
            {
                result.Exclude(block, "module_summary");
                continue;
            }

            Match heading = NumberedHeading.Match(line);
            if (heading.Success && heading.Groups[1].Value.Count(c => c == '.') <= 1)
            {
                collectingModuleTitle = false;
                SaveSkill();
                string code = heading.Groups[1].Value;
                string label = heading.Groups[2].Success ? heading.Groups[2].Value : heading.Groups[3].Value;
                if (!code.Contains('.'))
                {
                    competency = new Competency
                    {
                        Id = $"{module.Id}:competency:{code}",
                        Code = code,
                        Label = label
                    };
                    result.Exclude(block, "competency_heading_context");
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
                    skillBlocks = [block];
                    collectingSkillLabel = true;
                }
                continue;
            }

            if (collectingModuleTitle)
            {
                module = WithLabel(module, module.Label + " " + line);
                result.Exclude(block, "module_heading_continuation");
            }
            else if (skill is not null)
            {
                skillBlocks.Add(block);
                if (Regex.IsMatch(line, @"^(?:[•●\-]|o\s|\d+\.)")) collectingSkillLabel = false;
                if (collectingSkillLabel) skill = WithLabel(skill, skill.Label + " " + line);
            }
            else if (competency is not null)
            {
                competency = WithLabel(competency, competency.Label + " " + line);
                result.Exclude(block, "competency_heading_continuation");
            }
            else
            {
                result.UnclassifiedBlocks.Add(block);
            }
        }
        SaveSkill();

        foreach (var group in result.Chunks.GroupBy(chunk => chunk.Skill!.Id).Where(group => group.Count() > 1))
            foreach (Chunk chunk in group) chunk.ReviewIssues.Add("duplicate_skill");
        return result;
    }

    private static Competency WithLabel(Competency node, string label) => new()
    {
        Id = node.Id,
        Code = node.Code,
        Label = label
    };
}
