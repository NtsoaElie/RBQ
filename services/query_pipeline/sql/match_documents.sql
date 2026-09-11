-- Run once in the Supabase SQL editor.
-- Requires the `document_chunks` table written by services/ingestion_pipeline,
-- whose `embedding` column is a pgvector vector(1536) (text-embedding-3-small).

create extension if not exists vector;

create or replace function match_documents(
  query_embedding vector(1536),
  match_count int default 5
)
returns table (
  content text,
  filename text,
  page_number int,
  similarity float
)
language sql stable
as $$
  select
    document_chunks.content::text,
    document_chunks.filename::text,
    document_chunks.page_number::int,
    (1 - (document_chunks.embedding <=> query_embedding))::float as similarity
  from document_chunks
  order by document_chunks.embedding <=> query_embedding
  limit match_count;
$$;

-- Optional, once the table is large enough to need it:
-- create index on document_chunks
--   using ivfflat (embedding vector_cosine_ops) with (lists = 100);
