# RBQPrep — query pipeline (C#)

ASP.NET Core minimal API. Takes a question, embeds it with the same model the
ingestion pipeline used, retrieves the closest chunks from Supabase, and answers
from them. No NuGet dependencies beyond the framework.

## One-time setup

Run `sql/match_documents.sql` in the Supabase SQL editor. Without it every query
fails with `PGRST202: Could not find the function public.match_documents`.

## Run

```
dotnet run --launch-profile http     # http://localhost:5080
```

Config comes from the repo-root `.env` (shared with the Python pipeline):

| Variable | Required | Default |
| --- | --- | --- |
| `OPEN_API_KEY` | yes | — |
| `SUPABASE_URL` | yes | — |
| `SUPABASE_PRIVATE_KEY` | yes | — |
| `OPENAI_EMBEDDING_MODEL` | no | `text-embedding-3-small` |
| `OPENAI_CHAT_MODEL` | no | `gpt-4o-mini` |
| `WEB_ORIGIN` | no | `http://localhost:5173` |

`OPENAI_EMBEDDING_MODEL` must match what the ingestion pipeline used, or the
stored vectors are not comparable to the query vector.

## API

`GET /api/health` -> `{ "status": "ok" }`

`POST /api/ask`

```json
{ "question": "Quelles sont les compétences requises ?", "matchCount": 5 }
```

```json
{
  "answer": "...",
  "sources": [
    { "content": "...", "filename": "ADM_ProfilDeCompetences.pdf", "pageNumber": 5, "similarity": 0.82 }
  ]
}
```

## Layout

| File | Role |
| --- | --- |
| `Program.cs` | wiring: env, CORS, HTTP clients, endpoints |
| `src/RagService.cs` | the pipeline: embed -> retrieve -> answer |
| `src/OpenAiClient.cs` | embeddings + chat completions |
| `src/SupabaseClient.cs` | vector search via the `match_documents` RPC |
| `src/JsonHttp.cs` | the one JSON POST helper both clients use |
| `src/DotEnv.cs` | reads the repo-root `.env` |
| `src/Models.cs` | request/response records |
