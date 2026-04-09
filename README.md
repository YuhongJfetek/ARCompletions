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

## 向量相似度閾值與詳細計分日誌

建議設定（來自 1k 樣本分析）:

- `bot.embedding.directLow`: 建議預設為 `0.012`。
	- 備選：`0.0065`（提高召回），或 `0.015-0.02`（提高精準）。
- 開啟詳細計分日誌: 設環境變數 `DETAILED_SCORING_LOGS=true`。

用途與部署建議:

- `bot.embedding.directLow` 作為向量相似度「直接比對」下限，落在此值以上的候選才會視為可能命中。
- 建議把此值放在集中設定或環境變數中以便審計與回滾。範例（Windows PowerShell）:

```powershell
$env:bot_embedding_directLow = '0.012'
```

- 建議同時開啟詳細計分日誌以便診斷：

```powershell
$env:DETAILED_SCORING_LOGS = 'true'
```

驗證步驟:

- 在相同負載下跑 1,000 筆請求，執行 `scripts/analyze_scores` 取得 p50/p90/p95 與直方圖，並比對調整前後結果。
- 若 p90 與基準比較變化超過 ±10% 或零分率突增，請回退設定並調查 embeddings 來源或快取情況。

