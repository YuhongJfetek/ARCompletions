# ARCompletions — Local development setup

Quick steps to get a local development environment running.

1. Restore and build

```bash
dotnet restore
dotnet build
```

2. Configure secrets (recommended)

This project reads OpenAI keys from `Embedding:OpenAiApiKey` in configuration or from the `OPENAI_API_KEY` environment variable.

Use `dotnet user-secrets` for local development (recommended):

```powershell
cd path\to\ARCompletions
dotnet user-secrets init
dotnet user-secrets set "Embedding:OpenAiApiKey" "sk-..."
```

Or set environment variable (PowerShell):

```powershell
$env:OPENAI_API_KEY = "sk-..."
```

3. Database (SQLite local)

By default the app will use a SQLite DB at `Data/ARCompletions.db`. To override:

```powershell
$env:DB_PATH = "C:\path\to\my\ARCompletions.db"
```

4. Run migrations (optional)

To apply EF migrations on startup set `RUN_MIGRATIONS=true`:

```powershell
$env:RUN_MIGRATIONS = "true"
dotnet run
```

5. Quick dev helper scripts

See `scripts/setup-dev.ps1` (Windows) or `scripts/setup-dev.sh` (macOS/Linux) to bootstrap user-secrets and example env variables.

Security note: Do NOT commit real API keys to source control. Use user-secrets or environment variables.

See docs/OPENAI.md for more details about embedding configuration.
