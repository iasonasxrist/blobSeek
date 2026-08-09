# BlobSeek

BlobSeek is a self-hosted full-text search application for Azure Blob Storage. It crawls one or more blob containers, extracts text from supported files, maintains an incremental local index, and provides a fast React search interface for names, paths, metadata, and document contents.

This project is a from-scratch .NET 8 and React rebuild inspired by the original product and UI reference: [haseeb-140/bloblens](https://github.com/haseeb-140/bloblens). It preserves the useful product behavior while replacing the original FastAPI/Meilisearch stack with ASP.NET Core, PostgreSQL, and Lucene.NET.

## Highlights

- Index one or more Azure Blob Storage accounts and containers.
- Avoid re-downloading unchanged blobs by comparing ETags.
- Store indexing state, failures, leases, and run history in PostgreSQL.
- Search with an embedded Lucene.NET index; no external search service required.
- Extract text from PDF, DOCX, and common text/code formats.
- Keep search requests fast by querying only the existing index.
- Generate short-lived SAS download links on demand.
- Run locally or in production with Docker Compose.
- Use .NET configuration providers and environment variables for secrets.
- Ship with backend and frontend test scaffolding plus GitHub Actions CI.

## Tech Stack

| Area            | Technology                                            |
| --------------- | ----------------------------------------------------- |
| Backend         | .NET 8, ASP.NET Core Minimal APIs, C#                 |
| Search          | Lucene.NET                                            |
| State           | EF Core with SQLite                                   |
| Storage         | Azure.Storage.Blobs, Azure.Identity                   |
| Background work | ASP.NET Core `BackgroundService`                      |
| Observability   | Serilog, health checks, OpenAPI                       |
| Frontend        | React, TypeScript, Vite, TanStack Query, React Router |
| Styling         | Tailwind CSS, lucide-react icons                      |
| Tests           | xUnit, Vitest, React Testing Library, Playwright      |

## Architecture

```text
Azure Blob Storage
        |
        v
BlobSeek Worker
        |
        | list blobs
        | compare ETag / metadata
        | download changed supported blobs
        | extract text
        v
Lucene.NET Search Index

PostgreSQL
  |
  | blob state, failures, leases, run history
  v

React UI -> ASP.NET Core API -> Lucene.NET index
```

The API does not download or parse blobs during search. Search requests only query the existing Lucene index.

## Repository Layout

```text
.
├── BlobSeek.sln
├── backend/
│   ├── src/
│   │   ├── BlobSeek.Api/
│   │   ├── BlobSeek.Application/
│   │   ├── BlobSeek.Domain/
│   │   ├── BlobSeek.Infrastructure/
│   │   └── BlobSeek.Worker/
│   └── tests/
│       ├── BlobSeek.UnitTests/
│       ├── BlobSeek.IntegrationTests/
│       └── BlobSeek.ArchitectureTests/
├── frontend/
├── deploy/nginx/
├── docker-compose.yml
└── .github/workflows/ci.yml
```

## Quickstart With Docker

Copy the example environment file:

```bash
cp .env.example .env
```

Edit `.env` and set your Azure Storage connection information:

```text
BlobSeek__Storage__Accounts__0__Name=primary
BlobSeek__Storage__Accounts__0__ConnectionString=DefaultEndpointsProtocol=https;...
BlobSeek__Storage__Accounts__0__Containers__0=documents
ConnectionStrings__BlobSeek=Host=postgres;Port=5432;Database=blobseek;Username=blobseek;Password=blobseek
```

Start the stack:

```bash
docker compose up --build
```

Open:

```text
http://localhost:8000
```

The worker starts indexing immediately. Search results appear as blobs are discovered and indexed.

## Local Development

Run the API:

```bash
dotnet restore BlobSeek.sln
dotnet run --project backend/src/BlobSeek.Api
```

Run the worker in another terminal:

```bash
dotnet run --project backend/src/BlobSeek.Worker
```

Run the frontend:

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server runs at `http://localhost:5173` and proxies `/api` to `http://localhost:8080`.

## Configuration

BlobSeek uses the .NET options pattern, so configuration can come from JSON files, environment variables, user secrets, or your hosting platform.

Common settings:

<<<<<<< Updated upstream
| Setting                                      | Default        | Purpose                                 |
| -------------------------------------------- | -------------- | --------------------------------------- |
| `BlobSeek__Storage__SasExpiryMinutes`        | `60`           | Download link lifetime                  |
| `BlobSeek__Indexing__BatchSize`              | `100`          | Commit/save cadence                     |
| `BlobSeek__Indexing__MaxConcurrency`         | `4`            | Concurrent blob processing              |
| `BlobSeek__Indexing__PollingIntervalSeconds` | `300`          | Worker delay between sync passes        |
| `BlobSeek__Indexing__MaxBlobMegabytes`       | `50`           | Metadata-only threshold for large blobs |
| `BlobSeek__Indexing__MaxTextCharacters`      | `50000`        | Extracted text cap per blob             |
| `BlobSeek__Indexing__DataDirectory`          | `/data`        | SQLite and operational data path        |
| `BlobSeek__Search__IndexDirectory`           | `/data/lucene` | Lucene index path                       |
=======
| Setting | Default | Purpose |
| --- | --- | --- |
| `BlobSeek__Storage__SasExpiryMinutes` | `60` | Download link lifetime |
| `BlobSeek__Indexing__BatchSize` | `100` | Commit/save cadence |
| `BlobSeek__Indexing__MaxConcurrency` | `4` | Concurrent blob processing |
| `BlobSeek__Indexing__PollingIntervalSeconds` | `300` | Worker delay between sync passes |
| `BlobSeek__Indexing__MaxBlobMegabytes` | `50` | Metadata-only threshold for large blobs |
| `BlobSeek__Indexing__MaxTextCharacters` | `50000` | Extracted text cap per blob |
| `BlobSeek__Indexing__DataDirectory` | `/data` | Lucene and operational file data path |
| `BlobSeek__Search__IndexDirectory` | `/data/lucene` | Lucene index path |
>>>>>>> Stashed changes

You may use `ServiceUri` instead of `ConnectionString` to authenticate with `DefaultAzureCredential`:

```text
BlobSeek__Storage__Accounts__0__ServiceUri=https://myaccount.blob.core.windows.net
```

SAS download links require credentials that can sign SAS URLs. Managed identity is suitable for indexing, but may need a separate download strategy if the credential cannot generate SAS links.

## Supported Content

BlobSeek indexes metadata for every discovered blob:

- storage account
- container
- blob name and full path
- extension
- content type
- size
- last modified timestamp
- stable document ID
- blob URI

It extracts searchable text from:

- PDF files
- DOCX files
- common UTF-8 text/code formats including `txt`, `md`, `csv`, `json`, `xml`, `html`, `sql`, `py`, `js`, `ts`, `tsx`, `cs`, `java`, `go`, `rb`, `php`, `sh`, `ps1`, `css`, and similar files

Files larger than `BlobSeek__Indexing__MaxBlobMegabytes` are indexed by metadata only.

## API

| Method | Route                                                                          | Description                         |
| ------ | ------------------------------------------------------------------------------ | ----------------------------------- |
| `GET`  | `/api/search?q=invoice&container=uploads&ext=pdf&sort=last_modified:desc`      | Search indexed blobs                |
| `GET`  | `/api/stats`                                                                   | Read indexing status and statistics |
| `POST` | `/api/index/run`                                                               | Trigger an indexing pass            |
| `GET`  | `/api/download?storageAccount=primary&container=uploads&path=2026/invoice.pdf` | Create a short-lived download link  |
| `GET`  | `/health`                                                                      | Health check endpoint               |

Swagger UI is available in development.

## Testing

Backend:

```bash
dotnet test BlobSeek.sln
```

Frontend:

```bash
cd frontend
npm test
npm run build
```

End-to-end tests:

```bash
cd frontend
npx playwright install
npm run test:e2e
```

## Deployment Notes

- Keep BlobSeek behind your network boundary, VPN, or reverse proxy.
- Do not commit `.env`; use environment variables or platform secrets in production.
- Persist the `blobseek_postgres` and `blobseek_data` Docker volumes so PostgreSQL state and the Lucene index survive restarts.
- Run only one worker per indexing scope unless you introduce a stronger distributed lease.
- The browser never receives storage credentials, only search results and short-lived download URLs.

## License

MIT
