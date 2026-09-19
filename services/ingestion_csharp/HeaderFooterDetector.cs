using System.Text.RegularExpressions;

namespace Rbq.Ingestion;

public sealed class HeaderFooterDetector
{
    public HashSet<string> FindExcludedBlockIds(IReadOnlyList<TextBlock> blocks)
    {
        var excluded = new HashSet<string>();
        int pageCount = blocks.Select(block => block.PageNumber).Distinct().Count();
        int minimumRepeats = Math.Max(3, (int)Math.Ceiling(pageCount * 0.5));

        // Repetition alone is insufficient: text must also occupy a stable margin.
        var candidates = blocks.Where(IsMargin).Where(block => !IsStructuralHeading(block.Text));
        var groups = candidates.GroupBy(block => new
        {
            Text = Normalize(block.Text),
            TopMargin = block.Bounds!.Top < 0.10
        });

        foreach (var group in groups)
        {
            if (group.Select(block => block.PageNumber).Distinct().Count() < minimumRepeats)
                continue;
            if (group.Max(block => block.Bounds!.Top) - group.Min(block => block.Bounds!.Top) > 0.025)
                continue;
            if (group.Max(block => block.Bounds!.Left) - group.Min(block => block.Bounds!.Left) > 0.05)
                continue;
            foreach (TextBlock block in group)
                excluded.Add(block.Id);
        }

        foreach (TextBlock block in candidates)
        {
            string value = block.Text.Trim();
            // A numeric value in the body must never be removed as a page number.
            if (value == block.PageNumber.ToString())
                excluded.Add(block.Id);
        }
        return excluded;
    }

    private static bool IsMargin(TextBlock block)
    {
        return block.Bounds is not null && (block.Bounds.Bottom < 0.10 || block.Bounds.Top > 0.90);
    }

    private static bool IsStructuralHeading(string text)
    {
        return Regex.IsMatch(text, @"^(Module|Chapitre|Section|Article)\b", RegexOptions.IgnoreCase)
            || text.Contains("compétence", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Habiletés", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToUpperInvariant();
}
