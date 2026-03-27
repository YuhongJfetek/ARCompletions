Environment & Configuration reference
=================================

Required environment variables and recommended configuration for running ARCompletions.

1) DATABASE_URL (PostgreSQL)
   - Format (example): postgres://username:password@host:5432/databasename
   - The app parses this and constructs an Npgsql connection string. In production (Render) set this env var.

2) OPENAI_API_KEY
   - Your OpenAI API key used by the backend embedding worker.
   - Can also be set in configuration under `OpenAI:ApiKey` or `Embedding:OpenAiApiKey` for development.

3) RUN_MIGRATIONS
   - true/false (default false). When true the app will call EF Core `Database.Migrate()` on startup.

4) PORT
   - Optional: override the HTTP port the app listens on.

5) JWT__Secret (optional)
   - If you later add JWT auth, use `JWT__Secret` (double underscore) to map to `Jwt:Secret` in configuration.

6) Other useful settings
   - `Embedding:Model` can be set in appsettings or env with `Embedding__Model`.
   - `Admin:Username` and `Admin:Password` are present in `appsettings.Development.json` for local dev only.

Examples
--------
PowerShell (set for current session):

```powershell
$env:DATABASE_URL = "postgres://user:pass@db.example.com:5432/arcompletions"
$env:OPENAI_API_KEY = "sk-..."
$env:RUN_MIGRATIONS = "false"
dotnet run
```

Bash:

```bash
export DATABASE_URL='postgres://user:pass@db.example.com:5432/arcompletions'
export OPENAI_API_KEY='sk-...'
export RUN_MIGRATIONS=false
dotnet run
```

Security notes
--------------
- Never commit production secrets (OpenAI keys, DB passwords) into source control or `appsettings.Development.json`.
- For development, prefer `dotnet user-secrets` or environment variables.

Next steps
----------
- Optionally add `Jwt` and other secret-backed configuration to a secure secret store.
