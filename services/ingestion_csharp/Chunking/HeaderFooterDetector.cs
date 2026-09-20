using System.Text.RegularExpressions;

namespace Rbq.Ingestion;

public sealed class HeaderFooterDetector
{
    // Page coordinates run from 0 to 1, starting at the top-left corner.
    private const double TopMarginEnd = 0.10;
    private const double BottomMarginStart = 0.90;
    private const int MinimumRepeatedPages = 3;
    private const double RequiredPageFraction = 0.50;
    private const double MaximumVerticalPositionVariation = 0.025;
    private const double MaximumHorizontalPositionVariation = 0.05;

    public HashSet<string> FindExcludedBlockIds(IReadOnlyList<TextBlock> blocks)
    {
        var excludedBlockIds = new HashSet<string>();

        int documentPageCount = blocks.Select(block => block.PageNumber).Distinct().Count();
        int requiredPagesByFraction = (int)Math.Ceiling(documentPageCount * RequiredPageFraction);
        int requiredRepeatedPages = Math.Max(MinimumRepeatedPages, requiredPagesByFraction);

        var marginBlocks = new List<TextBlock>();

        foreach (TextBlock block in blocks)
        {
            bool isInPageMargin = IsInTopOrBottomMargin(block);
            bool isDocumentHeading = IsStructuralHeading(block.Text);

            if (isInPageMargin && !isDocumentHeading)
            {
                marginBlocks.Add(block);
            }
        }

        // Matching text at the top and bottom belongs to separate groups.
        var repeatedTextGroups = marginBlocks.GroupBy(block => new
        {
            Text = NormalizeTextForComparison(block.Text),
            IsTopMargin = block.Bounds!.Top < TopMarginEnd
        });

        foreach (var repeatedTextGroup in repeatedTextGroups)
        {
            int pagesWithThisText = repeatedTextGroup
                .Select(block => block.PageNumber)
                .Distinct()
                .Count();

            bool appearsOnEnoughPages = pagesWithThisText >= requiredRepeatedPages;

            if (!appearsOnEnoughPages)
            {
                continue;
            }

            double verticalPositionVariation =
                repeatedTextGroup.Max(block => block.Bounds!.Top) -
                repeatedTextGroup.Min(block => block.Bounds!.Top);

            double horizontalPositionVariation =
                repeatedTextGroup.Max(block => block.Bounds!.Left) -
                repeatedTextGroup.Min(block => block.Bounds!.Left);

            bool hasConsistentVerticalPosition =
                verticalPositionVariation <= MaximumVerticalPositionVariation;

            bool hasConsistentHorizontalPosition =
                horizontalPositionVariation <= MaximumHorizontalPositionVariation;

            if (!hasConsistentVerticalPosition || !hasConsistentHorizontalPosition)
            {
                continue;
            }

            foreach (TextBlock block in repeatedTextGroup)
            {
                excludedBlockIds.Add(block.Id);
            }
        }

        // Page numbers do not need to repeat, but must still be in a page margin.
        foreach (TextBlock block in marginBlocks)
        {
            string trimmedText = block.Text.Trim();
            string expectedPageNumber = block.PageNumber.ToString();
            bool containsOnlyCurrentPageNumber = trimmedText == expectedPageNumber;

            if (containsOnlyCurrentPageNumber)
            {
                excludedBlockIds.Add(block.Id);
            }
        }

        return excludedBlockIds;
    }

    private static bool IsInTopOrBottomMargin(TextBlock block)
    {
        if (block.Bounds is null)
        {
            return false;
        }

        bool isEntirelyInTopMargin = block.Bounds.Bottom < TopMarginEnd;
        bool isEntirelyInBottomMargin = block.Bounds.Top > BottomMarginStart;

        return isEntirelyInTopMargin || isEntirelyInBottomMargin;
    }

    private static bool IsStructuralHeading(string text)
    {
        bool startsWithSectionHeading = Regex.IsMatch(
            text,
            @"^(Module|Chapitre|Section|Article)\b",
            RegexOptions.IgnoreCase);

        bool mentionsCompetency = text.Contains("compétence", StringComparison.OrdinalIgnoreCase);
        bool mentionsSkills = text.Contains("Habiletés", StringComparison.OrdinalIgnoreCase);

        return startsWithSectionHeading || mentionsCompetency || mentionsSkills;
    }

    private static string NormalizeTextForComparison(string text)
    {
        string trimmedText = text.Trim();
        string textWithSingleSpaces = Regex.Replace(trimmedText, @"\s+", " ");

        return textWithSingleSpaces.ToUpperInvariant();
    }
}