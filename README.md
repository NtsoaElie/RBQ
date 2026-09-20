# RBQ

The backend uses C# and .NET 8 for document ingestion and querying. Both services
share the same Chunk and Competency classes in services/shared/Rbq.Contracts.

## Document ingestion

Install the .NET 8 SDK and run from the repository root:

```powershell
dotnet run --project services/ingestion_csharp -- ingest ADM_ProfilDeCompetences.pdf --document-id rbq-administration --profile-id ADM --source-type profile --output output/administration.json
```

Extraction does not require API credentials. Review the exported chunks before
uploading. Apply services/ingestion_csharp/Database/schema.sql in Supabase, set
OPENAI_API_KEY, SUPABASE_URL and SUPABASE_PRIVATE_KEY as environment variables,
then run:

```powershell
dotnet run --project services/ingestion_csharp -- upload output/administration.json
```

The ingestion CLI reads process environment variables; it does not load .env
files automatically. Never commit credentials.

See [ingestion instructions](services/ingestion_csharp/README.md) for reference
mapping, source versions, shared objects, validation and parser limitations.
Website crawling and quiz generation are not implemented yet.

## Query API and web app

- [Query API](services/query_pipeline/README.md)
- [Web app](web/README.md)

## Checks

```powershell
dotnet run --project tests/Rbq.Ingestion.Tests
dotnet build services/query_pipeline/Rbq.QueryApi.csproj
```

The ingestion checks mock external services and do not require credentials.
