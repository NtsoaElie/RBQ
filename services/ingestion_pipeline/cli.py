"""Run from repository root: python -m services.ingestion_pipeline.cli --help."""
import argparse
import json
from pathlib import Path
from .chunking import chunking
from .pdf_to_raw_text import pdf_to_raw_text


def write_json(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description='RBQ document ingestion')
    commands = parser.add_subparsers(dest='command', required=True)
    ingest = commands.add_parser('ingest')
    ingest.add_argument('file')
    ingest.add_argument('--profile-id', required=True)
    ingest.add_argument('--document-id', required=True)
    ingest.add_argument('--source-type', choices=['profile', 'reference'], required=True)
    ingest.add_argument('--source-url')
    ingest.add_argument('--skill-id', action='append', default=[])
    ingest.add_argument('--output', required=True)
    args = parser.parse_args()
    if args.command == 'ingest':
        path = Path(args.file)
        pages = pdf_to_raw_text(path) if path.suffix.lower() == '.pdf' else [(path.read_text(encoding='utf-8'), 1, path.name)]
        chunks = chunking(pages, profile_id=args.profile_id, document_id=args.document_id,
                           source_type=args.source_type, source_url=args.source_url, skill_ids=args.skill_id)
        if not chunks:
            raise ValueError('No skills/passages detected. Inspect extraction and document layout.')
        write_json(args.output, [chunk.to_dict() for chunk in chunks])
        print(f'{len(chunks)} chunks; {sum(bool(r.review_issues) for r in chunks)} require review')


if __name__ == '__main__':
    main()
