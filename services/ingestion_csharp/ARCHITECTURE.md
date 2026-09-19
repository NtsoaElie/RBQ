# How ingestion is organized

Start with `DocumentChunker.Parse()`. It is a coordinator, not a collection of
rules for every RBQ document title.

```text
Catalog entry (optional)
    -> PdfTextExtractor / plain-text input
    -> TextBlock objects with page geometry
    -> HeaderFooterDetector
    -> DocumentParserFactory
        -> CompetencyProfileParser
        -> LegalDocumentParser
        -> ExamInformationParser
        -> TechnicalDocumentParser
    -> ParsingResult (chunks + warnings + review/audit blocks)
    -> JSON report / validated embedding and upload
```

## Objects

- `DocumentSource`: catalog identity, document kind, title, URL and exam associations.
- `ExtractedDocument`: source metadata plus text blocks and a text-content version.
- `TextBlock`: one extracted line, stable source ID, page, optional bounds/font size,
  and a paragraph-start marker. Coordinates start at the top left and are normalized.
- `Chunk`: shared object used by ingestion and the query service. `SectionPath` and
  `ArticleNumber` describe the source; `SkillIds` describes learning associations.
- `ParsingResult`: chunks, warnings, unclassified blocks and exclusions with reasons.

`SourceType` describes the role of the source (profile/reference/exam information).
`DocumentKind` chooses structural parsing (profile/legal/exam/technical). A legal
document is reference material, but not every reference is a law.

## Why layout matters

Repeated words are not necessarily page headers. The detector requires repeated
text at a stable position in the top/bottom margins on at least three pages and
half the document. Module and table headings remain available to the parser.
Numeric body content is retained. Missing coordinates never trigger guessed
header removal. Removed blocks are recorded rather than discarded silently.

## Adding a format

Implement `IDocumentParser.Parse(document, options)` and register it in
`DocumentParserFactory`. Keep the generic coordinator unchanged. Build source
chunks through `ChunkFactory` to keep provenance, IDs and review flags consistent.
Unknown text goes to `UnclassifiedBlocks`. Recognized non-content goes to
`Exclude(block, reason)`. Never label unknown text as a competency merely because
it begins with a number.

Add representative fixtures: expected source spans, parent labels, page ranges,
headings repeated across pages and counterexamples such as legal citations inside
skill descriptions. Unclassified content blocks upload until it is resolved.

The example catalog is manual. HTML extraction, complete website discovery,
specialized legal HTML adapters and table reconstruction are future work; the
architecture now provides separate places to add them.
