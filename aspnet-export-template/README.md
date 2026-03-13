This is an ASP.NET Core Web API that mirrors the functionality of the TypeScript `coreapi-export-template` app.

- Configure environment variable `API_KEY` with your Front API key.
- Build and run with `dotnet run` in the `aspnet-export-template` folder.
- Endpoints:
  - `GET /api/export/inboxes` - list inboxes
  - `POST /api/export/inbox/{id}` - export conversations for an inbox (send `ExportOptions` JSON body to adjust options)
  - `POST /api/export/search` - export conversations found by search (send `SearchRequest` JSON body)

Exported files are written to `./export/...` relative to where the app runs.
