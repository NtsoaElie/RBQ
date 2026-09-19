"""Explicit, opt-in upload. Importing this module never writes to Supabase."""
import argparse
import json
import os
from pathlib import Path
from .embedding import embed_many
from .models import Chunk

def upload_chunks(chunks: list[Chunk], *, client=None, embedding_client=None):
    if any(c.review_issues for c in chunks):
        raise ValueError('Resolve flagged chunks before uploading')
    if not chunks:
        return 0
    if client is None:
        from dotenv import load_dotenv
        from supabase import create_client
        load_dotenv()
        url, key = os.getenv('SUPABASE_URL'), os.getenv('SUPABASE_PRIVATE_KEY')
        if not url or not key:
            raise ValueError('SUPABASE_URL and SUPABASE_PRIVATE_KEY are required')
        client = create_client(url, key)
    vectors = embed_many([c.embedding_text for c in chunks], client=embedding_client)
    rows = []
    for chunk, vector in zip(chunks, vectors, strict=True):
        rows.append(dict(chunk_id=chunk.chunk_id, content=chunk.embedding_text,
                         filename=chunk.filename, page_number=chunk.page_start,
                         embedding=vector, metadata=chunk.to_dict()))
    # Same document version is idempotent. Different versions are retained.
    for start in range(0, len(rows), 64):
        client.table('document_chunks').upsert(rows[start:start + 64], on_conflict='chunk_id').execute()
    return len(rows)

def main():
    parser = argparse.ArgumentParser(description='Upload reviewed chunk JSON to Supabase')
    parser.add_argument('file')
    args = parser.parse_args()
    data = json.loads(Path(args.file).read_text(encoding="utf-8"))
    chunks = [Chunk.from_dict(item) for item in data]
    print(f'Uploaded {upload_chunks(chunks)} chunks')

if __name__ == '__main__':
    main()
    

