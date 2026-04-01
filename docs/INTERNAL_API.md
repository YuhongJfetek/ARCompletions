Internal API summary

Purpose
- Consolidated internal endpoints: LineBot input/output are the only public-facing endpoints for bot integration. Other query endpoints are deprecated and internal callers should use service layer APIs.

Endpoints
- POST /internal/v1/linebot/input
  - Accepts multipart/form-data from Node/LINE webhook (file optional).
  - Persists an input MessageResult via `IMessageResultService.PersistResultAsync`.
  - Calls `IFaqQueryService.AnalyzeAsync` to run analysis (alias/route/handoff/embedding).
  - Returns: `{ traceId, inputSaved, analysis, driveFileId?, driveFileUrl? }`.

- POST /internal/v1/linebot/output
  - Accepts analysis result from Node (JSON).
  - Calls `IFaqQueryService.AnalyzeAsync` if needed, persists route via `IMessageRouteService` or result via `IMessageResultService`.
  - Returns route/persist info used to correlate pushes.

Deprecated public endpoints
- POST /api/faqs/query  => Deprecated. Use `IFaqQueryService` internally.
- POST /api/embeddings/query => Deprecated. Use `IEmbeddingService` internally.
- POST /internal/v1/bot/query => Deprecated. Use `IFaqQueryService.AnalyzeAsync` or `/internal/v1/linebot/input`.

Service migration
- Internal callers (Admin controllers, background workers) should obtain `IFaqQueryService` / `IEmbeddingService` via DI and call their methods directly instead of issuing HTTP requests to the deprecated controllers.

Auth
- `/internal/v1/*` is protected by `X-Internal-API-Key` middleware by default (see `Program.cs`). If Node/LINE cannot provide the header, add the key to the caller or configure a gateway/allowlist.

Notes
- Embedding generation still uses `IEmbeddingService` to call OpenAI embeddings; workers persist results to `EmbeddingItems`.
- After migration, deprecated controllers will return HTTP 410 and are hidden in Swagger.

Example: calling `IFaqQueryService` from an Admin controller

```csharp
public class MyAdminController : Controller
{
    private readonly IFaqQueryService _faqQuery;
    public MyAdminController(IFaqQueryService faqQuery)
    {
        _faqQuery = faqQuery;
    }

    public async Task<IActionResult> Analyze(string text)
    {
        var req = new MessageAnalyzeRequestDto { TraceId = Guid.NewGuid().ToString("N"), Text = text, NodeMeta = new Dictionary<string,string>{{"VendorId", "vendor1"}} };
        var resp = await _faqQuery.AnalyzeAsync(req);
        return Json(resp);
    }
}
```

Examples: curl and Node

curl (multipart/form-data input):

```bash
curl -X POST "https://your-host/internal/v1/linebot/input" \
  -H "X-Internal-API-Key: ${BACKEND_API_KEY}" \
  -F "VendorId=vendor1" \
  -F "ExternalUserId=U123" \
  -F "MessageType=text" \
  -F "MessageText=hello" \
  -F "TraceId=optional-trace-id"
```

Node (axios) example:

```js
const axios = require('axios');
const FormData = require('form-data');

async function sendInput() {
  const form = new FormData();
  form.append('VendorId', 'vendor1');
  form.append('ExternalUserId', 'U123');
  form.append('MessageType', 'text');
  form.append('MessageText', 'hello');
  // optional file: form.append('File', fs.createReadStream('./file.jpg'));

  const resp = await axios.post('https://your-host/internal/v1/linebot/input', form, {
    headers: {
      ...form.getHeaders(),
      'X-Internal-API-Key': process.env.BACKEND_API_KEY
    },
    maxContentLength: Infinity,
    maxBodyLength: Infinity
  });
  console.log(resp.status, resp.data);
}

sendInput().catch(console.error);
```
