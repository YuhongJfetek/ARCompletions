# OPENAI / Embeddings Configuration

This project supports computing embeddings using OpenAI's Embeddings API.

Configuration options (preferred sources in order):

- appsettings.json / appsettings.Development.json under the `Embedding` section
- Environment variable `OPENAI_API_KEY` (if not set in config)
- dotnet user-secrets (for local development)

Example `appsettings.Development.json`:

```json
{
  "Embedding": {
    "OpenAiApiKey": "",
    "Model": "text-embedding-3-small"
  }
}
```

To set the key locally using user-secrets:

```bash
cd path/to/ARCompletions
dotnet user-secrets init
dotnet user-secrets set "Embedding:OpenAiApiKey" "sk-..."
```

Or set the environment variable in your hosting environment:

Windows (PowerShell):

```powershell
$env:OPENAI_API_KEY = "sk-..."
```

Linux/macOS:

```bash
export OPENAI_API_KEY="sk-..."
```

Note: The development `appsettings.Development.json` file contains an empty placeholder. Do NOT commit real API keys to source control.
