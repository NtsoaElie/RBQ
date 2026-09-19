# RBQ ingestion

Python handles PDF extraction, structured chunking, embeddings, and Supabase uploads.
Quiz generation belongs in the C# service and is not implemented by this Python package.

From the repository root, install the existing dependencies with `uv sync`, or
Python 3.14+ and `pip install openai pymupdf python-dotenv supabase`.
Prefix the commands below with `uv run` if using uv.

## Ingest your documents

```powershell
python -m services.ingestion_pipeline.cli ingest ADM_ProfilDeCompetences.pdf --profile-id ADM --document-id rbq-administration --source-type profile --output output/administration.json
python -m services.ingestion_pipeline.cli ingest reference-bilan.pdf --profile-id ADM --document-id reference-bilan --source-type reference --skill-id ADM:skill:1.2 --output output/bilan.json
```

Use a reference excerpt whose entire content supports the supplied skill(s).
`--skill-id` can be repeated. Arbitrary reference PDFs are not automatically
classified. Inspect the extracted text and mapping before generating questions.
UTF-8 text inputs are also accepted (page 1 means the text file, not a PDF page).
Use `--source-url` to retain a source URL.

Profiles produce one `Chunk` object per required skill, retaining its module,
competency, source code, breadcrumb and physical PDF page range. Short skills
stay short. References are grouped by paragraph using a 2,000-character budget;
oversized paragraphs stay intact and are flagged, rather than severing a list
or exception. This is not a tokenizer-based 500-token guarantee.

`models.py` defines the typed `Chunk` and `Competency` dataclasses. Use
attributes such as `chunk.content`, `chunk.skill.id`, and `chunk.page_start`.
Modules, competencies, and skills are immutable `Competency` objects.
`chunk.to_dict()` and `Chunk.from_dict()` handle JSON/database boundaries,
preserving the existing exported field names and Supabase metadata shape.

IDs such as `ADM:skill:1.2` survive content edits. Document versions are content
hashes, and chunk IDs change with the source revision. If RBQ renumbers skills,
review the taxonomy and create an explicit migration; IDs alone cannot establish
equivalence between renumbered objectives.

Set `OPENAI_API_KEY` in your environment or local `.env` for embedding uploads.
`OPEN_API_KEY` remains a legacy fallback. Never commit credentials.

## Supabase (optional)

Apply `services/ingestion_pipeline/schema.sql` manually to add `chunk_id` and
`metadata` to the existing `document_chunks` table. Set `SUPABASE_URL` and
`SUPABASE_PRIVATE_KEY` on the backend, then run:

```powershell
python -m services.ingestion_pipeline.vector_store_to_supabase output/administration.json
```

The uploader batches embeddings and upserts by chunk ID. It refuses flagged
records. Existing Q&A columns remain available, with breadcrumbs in `content`.
Repeated uploads of the same version are idempotent. Older versions and legacy
rows remain stored; the existing Q&A RPC searches all versions. Curate active
versions before relying on it for version-sensitive answers. The migration enables RLS without
adding public policies; backend service-role access is required.

## Validation and known limits

```powershell
python -m unittest discover -s tests -v
```

The parser was exercised on the official 24-page Administration profile:
174 skills across four modules and 24 competency elements. Automatic checks
reported no missing-parent, duplicate-skill or oversized-unit flags. This is
not a full manual content audit. Other profile layouts require validation;
scanned pages need OCR, and PDF reading order is retained rather than inferred.

Tests cover page continuations, multiline headings, summary exclusion, stable
skill IDs, duplicate/missing parents, empty input, reference boundaries,
embedding order and upload metadata. Live embedding calls and Supabase
uploads require credentials and were not run here.
