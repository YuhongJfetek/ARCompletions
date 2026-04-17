// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Security.Claims;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ARCompletions.Config;
using Microsoft.OpenApi.Models;

// 第一行就設定 ASPNETCORE_URLS，確保優先於 CreateBuilder 的環境變數解析
// Support platforms (like Render) that set `HTTP_PORTS` or `PORT`.
// Prefer `PORT` (explicit platform port) → `HTTP_PORTS` (first entry). If neither is set, abort startup
// to avoid silently binding to an incorrect default port.
var portFromHttpPorts = Environment.GetEnvironmentVariable("HTTP_PORTS");
var portFromPort = Environment.GetEnvironmentVariable("PORT");

// Debug: print raw env values so platform logs show what was provided
Console.WriteLine($"ENV HTTP_PORTS={(string.IsNullOrWhiteSpace(portFromHttpPorts) ? "<empty>" : portFromHttpPorts)}; PORT={(string.IsNullOrWhiteSpace(portFromPort) ? "<empty>" : portFromPort)}; ASPNETCORE_URLS={(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "<empty>")}");
string selectedPort = null;
string portSource = null;

// Prefer PORT when provided by platform/runtime (Render typically injects PORT)
if (!string.IsNullOrWhiteSpace(portFromPort))
{
    selectedPort = portFromPort.Trim();
    portSource = "PORT";
}

// Otherwise fall back to first HTTP_PORTS entry
if (string.IsNullOrWhiteSpace(selectedPort) && !string.IsNullOrWhiteSpace(portFromHttpPorts))
{
    selectedPort = portFromHttpPorts.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    portSource = "HTTP_PORTS";
}

if (!string.IsNullOrWhiteSpace(portFromPort) && !string.IsNullOrWhiteSpace(portFromHttpPorts))
{
    var firstHttp = portFromHttpPorts.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
    if (!string.Equals(firstHttp, portFromPort, StringComparison.Ordinal))
    {
        Console.WriteLine($"WARNING: Both PORT={portFromPort} and HTTP_PORTS={portFromHttpPorts} are set; using PORT={portFromPort}.");
    }
}

if (string.IsNullOrWhiteSpace(selectedPort))
{
    Console.Error.WriteLine("ERROR: Missing required environment variable: PORT or HTTP_PORTS. Aborting startup to avoid binding to an incorrect default port.");
    throw new InvalidOperationException("Missing required environment variable: PORT or HTTP_PORTS");
}

var aspnetUrlsBefore = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{selectedPort}");
Console.WriteLine($"ASPNETCORE_URLS before={aspnetUrlsBefore ?? "<empty>"}; after={Environment.GetEnvironmentVariable("ASPNETCORE_URLS")}");
Console.WriteLine($"Binding to port: {selectedPort} (from {portSource})");

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// CORS：允許 swagger 與本機前端
// ------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ------------------------
// 資料庫設定：優先 Render Postgres（DATABASE_URL），否則回退 SQLite（DB_PATH）
// ------------------------
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var isPostgres = !string.IsNullOrWhiteSpace(databaseUrl);

// Require DATABASE_URL (always use Postgres in production on Render)
if (!isPostgres)
{
    var message = "DATABASE_URL environment variable is required. Set it to your PostgreSQL connection string.";
    Console.WriteLine(message);
    throw new InvalidOperationException(message);
}

if (isPostgres)
{
    // ---- PostgreSQL（Render）----
    // 例：postgres://user:pass@host:5432/dbname
    var uri = new Uri(databaseUrl!);
    var userInfoParts = uri.UserInfo.Split(':', 2);
    var user = WebUtility.UrlDecode(userInfoParts[0]);
    var pass = userInfoParts.Length > 1 ? WebUtility.UrlDecode(userInfoParts[1]) : "";
    var host = uri.Host;
    var portNum = uri.Port == -1 ? 5432 : uri.Port;
    var dbName = uri.AbsolutePath.Trim('/');

    // 手動組裝連線字串（不使用 NpgsqlConnectionStringBuilder）
    var pgConn =
        $"Host={host};Port={portNum};Database={dbName};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";

    builder.Services.AddDbContext<ARCompletionsContext>(opt =>
        opt.UseNpgsql(pgConn)); // ← 已移除 .MigrationsAssembly(...)
    // Also register a DbContextFactory so callers can create short-lived contexts for parallel/async work
    builder.Services.AddDbContextFactory<ARCompletionsContext>(opt =>
        opt.UseNpgsql(pgConn));
}

// 其他服務
builder.Services.AddControllersWithViews();
// memory cache for optional in-memory state and caching
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ARCompletions API v1",
        Version = "v1"
    });

    // 讓需要呼叫 /internal/v1/* 的客戶端，可以在 Swagger UI 透過 Authorize 輸入 X-Internal-API-Key
    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = "X-Internal-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Internal API key for /internal/v1/* endpoints",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "InternalApiKey"
        }
    };

    c.AddSecurityDefinition("InternalApiKey", apiKeyScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { apiKeyScheme, Array.Empty<string>() }
    });
    // Remove specific internal endpoints from Swagger (if present)
    c.DocumentFilter<ARCompletions.Swagger.Filters.RemovePathsFilter>();
});

// 認證與授權：使用 Cookie 登入平台帳號與廠商帳號
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Account/Login";
        options.AccessDeniedPath = "/Admin/Account/AccessDenied";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Platform", policy =>
    {
        policy.RequireClaim("Role", "Platform");
    });

    options.AddPolicy("Vendor", policy =>
    {
        policy.RequireClaim("Role", "Vendor");
    });
});
// bind embedding config (can be set via appsettings or secrets / env vars)
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
// typed HttpClient for OpenAI requests (used by AnalysisWorker)
builder.Services.AddHttpClient("OpenAI", c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
// HttpClient for external services (LINE)
builder.Services.AddHttpClient();
// Embedding service for bot_* FAQ embeddings
// Previously registered as Singleton but it consumes the scoped ARCompletionsContext.
// Register as Scoped to avoid DI lifecycle issues when resolving DbContext.
builder.Services.AddScoped<ARCompletions.Services.IEmbeddingService, ARCompletions.Services.EmbeddingService>();
builder.Services.AddScoped<ARCompletions.Services.IEmbeddingRebuildService, ARCompletions.Services.EmbeddingRebuildService>();
// Drive service for file uploads (uses GOOGLE_SERVICE_ACCOUNT_KEY + GOOGLE_DRIVE_FOLDER_ID)
// GoogleDriveService depends on scoped services (DbContext), register as Scoped.
builder.Services.AddScoped<ARCompletions.Services.IDriveService, ARCompletions.Services.GoogleDriveService>();
// Disambiguation service (handles numeric selection and atomic state updates)
builder.Services.AddScoped<ARCompletions.Services.IDisambiguationService, ARCompletions.Services.DisambiguationService>();
// New services
builder.Services.AddScoped<ARCompletions.Services.ITextProcessingService, ARCompletions.Services.TextProcessingService>();
builder.Services.AddScoped<ARCompletions.Services.IQueryHintsService, ARCompletions.Services.QueryHintsService>();
builder.Services.AddScoped<ARCompletions.Services.IPrefilterService, ARCompletions.Services.PrefilterService>();
builder.Services.AddScoped<ARCompletions.Services.IStateService, ARCompletions.Services.StateService>();
builder.Services.AddScoped<ARCompletions.Services.IAliasService, ARCompletions.Services.AliasService>();
builder.Services.AddScoped<ARCompletions.Services.IFaqService, ARCompletions.Services.FaqService>();
builder.Services.AddScoped<ARCompletions.Services.IEmbeddingRetrievalService, ARCompletions.Services.EmbeddingRetrievalService>();
builder.Services.AddScoped<ARCompletions.Services.IScoringService, ARCompletions.Services.ScoringService>();
builder.Services.AddScoped<ARCompletions.Services.ICandidateBuilderService, ARCompletions.Services.CandidateBuilderService>();
builder.Services.AddScoped<ARCompletions.Services.IRouteLoggingService, ARCompletions.Services.RouteLoggingService>();
builder.Services.AddScoped<ARCompletions.Services.IResponseBuilder, ARCompletions.Services.ResponseBuilder>();
// DB-backed logger for services that previously used ILogger
builder.Services.AddScoped<ARCompletions.Services.IDbLogger, ARCompletions.Services.DbLogger>();
// Register the real buffered background logger as a singleton and hosted service
// Use the more conventional registration to avoid resolving the instance manually during host start.
builder.Services.AddSingleton<ARCompletions.Services.IBufferedAppLogger, ARCompletions.Services.BufferedAppLogger>();
builder.Services.AddHostedService<ARCompletions.Services.BufferedAppLogger>();
// Embedding update queue is kept, but no background worker is registered
builder.Services.AddSingleton<ARCompletions.Services.IEmbeddingUpdateQueue, ARCompletions.Services.EmbeddingUpdateQueue>();
// Embeddings cache (process-level)
builder.Services.AddSingleton<ARCompletions.Services.IEmbeddingsCache, ARCompletions.Services.EmbeddingsCache>();
// Distributed lock implementation (Postgres advisory lock)
// Register as Transient and expose a factory so callers can create a fresh lock per call.
builder.Services.AddTransient<ARCompletions.Services.IDistributedLock, ARCompletions.Services.PostgresAdvisoryLock>();
builder.Services.AddScoped<Func<ARCompletions.Services.IDistributedLock>>(sp => () => sp.GetRequiredService<ARCompletions.Services.IDistributedLock>());

var app = builder.Build();

// 在開發環境啟用詳細例外頁，方便本地除錯（Development 環境才會啟用）
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Log EF Core assembly version for debugging migration/runtime differences
Console.WriteLine("EF VERSION: " + typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.GetName().Version);

// ------------------------
// 健康檢查端點（Render 可用來探活）
// ------------------------
app.MapGet("/healthz", () => Results.Ok("ok"));

// ------------------------
// 啟動時是否自動遷移（預設關閉，先讓服務穩定啟動）
// 需要升版時再把 RUN_MIGRATIONS 設為 true
// ------------------------
var runMigrations = (Environment.GetEnvironmentVariable("RUN_MIGRATIONS") ?? "false")
                    .Equals("true", StringComparison.OrdinalIgnoreCase);

// startup logs removed: DB Provider and RUN_MIGRATIONS are printed to Console instead
Console.WriteLine($"DB Provider: {(isPostgres ? "PostgreSQL" : "SQLite")}");
Console.WriteLine($"Auto-migrate on startup (RUN_MIGRATIONS): {runMigrations}");

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();

if (runMigrations)
{
    try
    {
        db.Database.Migrate();
        Console.WriteLine("Migrations applied successfully.");
        Console.WriteLine("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to apply migrations on startup: {ex.Message}");
        Console.WriteLine(ex.ToString());
    }
}

// Optional one-shot seed from original JFETEK JSON files
var seedFromJson = (Environment.GetEnvironmentVariable("SEED_BOT_FROM_JSON") ?? "false")
    .Equals("true", StringComparison.OrdinalIgnoreCase);

if (seedFromJson)
{
    var dataRoot = Environment.GetEnvironmentVariable("BOT_DATA_ROOT")
                   ?? "C:\\Users\\jamie\\Downloads\\linebot-jfetek-bot(1)\\app\\data";

    Console.WriteLine($"Seeding bot_* tables from JSON at: {dataRoot}");
    try
    {
        await BotJsonSeeder.SeedAsync(db, dataRoot);
        Console.WriteLine("Bot JSON data seeded successfully. Exiting application.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Bot JSON data seeding failed: " + ex.Message);
        Console.WriteLine(ex.ToString());
        throw;
    }

    return;
}
else
{
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("EnsureCreated executed (database exists or was created).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to ensure database creation on startup: {ex.Message}");
        Console.WriteLine(ex.ToString());
    }
}

// ------------------------
// 中介層與靜態檔案
// ------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ARCompletions API v1");
    // c.RoutePrefix = string.Empty; // 若要根路徑顯示 Swagger UI 可解註
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Internal API key middleware: protect routes under /internal/v1
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/internal/v1", StringComparison.OrdinalIgnoreCase))
    {
        var headerKey = context.Request.Headers["X-Internal-API-Key"].FirstOrDefault();
        var expected = Environment.GetEnvironmentVariable("BACKEND_API_KEY") ?? builder.Configuration["BACKEND_API_KEY"];
        // allow a frontend token as alternative (for webhook-forwarding frontends)
        var headerFrontend = context.Request.Headers["X-Frontend-Token"].FirstOrDefault();
        var frontendExpected = Environment.GetEnvironmentVariable("FRONTEND_TOKEN") ?? builder.Configuration["FRONTEND_TOKEN"];
        var bypass = (Environment.GetEnvironmentVariable("ALLOW_INTERNAL_API_WITHOUT_KEY") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        // 若未設定 BACKEND_API_KEY，或明確允許略過驗證，則不檢查 Header（方便開發 / 測試環境使用 Swagger）。
        if (!bypass && !string.IsNullOrWhiteSpace(expected))
        {
            var ok = !string.IsNullOrWhiteSpace(headerKey) && string.Equals(headerKey, expected, StringComparison.Ordinal);
            var okFrontend = !string.IsNullOrWhiteSpace(headerFrontend) && !string.IsNullOrWhiteSpace(frontendExpected) && string.Equals(headerFrontend, frontendExpected, StringComparison.Ordinal);
            if (!ok && !okFrontend)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { success = false, error = new { code = "Unauthorized", message = "Missing or invalid X-Internal-API-Key or X-Frontend-Token" } });
                return;
            }
        }
    }

    await next();
});

// wwwroot
app.UseStaticFiles();

// /Image 資料夾靜態檔案
var imagePath = Path.Combine(builder.Environment.ContentRootPath, "Image");
if (Directory.Exists(imagePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imagePath),
        RequestPath = "/Image"
    });
}
else
{
    Console.WriteLine($"Static image path not found: {imagePath}");
}

// Removed `UseHttpsRedirection` for Render deployment (platform handles TLS)

// Area route for admin MVC
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

try
{
    Console.WriteLine("=== Starting host with explicit StartAsync ===");
    await app.StartAsync();

    // Try to surface the actual addresses the server is listening on as early as possible
    var addressesFeature = app.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature))
                           as Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature;
    if (addressesFeature != null && addressesFeature.Addresses != null && addressesFeature.Addresses.Any())
    {
        Console.WriteLine("Now listening on: " + string.Join(", ", addressesFeature.Addresses));
    }
    else if (app.Urls != null && app.Urls.Count > 0)
    {
        Console.WriteLine("Now listening on (from app.Urls): " + string.Join(", ", app.Urls));
    }
    else
    {
        Console.WriteLine("No server addresses reported yet.");
    }

    // Additional diagnostic: attempt a local TCP probe to confirm the server socket is accepting
    if (int.TryParse(selectedPort, out var _probePort) && _probePort > 0)
    {
        var probeSuccess = false;
        for (var attempt = 0; attempt < 10 && !probeSuccess; attempt++)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var task = tcp.ConnectAsync("127.0.0.1", _probePort);
                var completed = await Task.WhenAny(task, Task.Delay(500)).ConfigureAwait(false);
                if (completed == task && tcp.Connected)
                {
                    probeSuccess = true;
                    Console.WriteLine($"Port probe success: 127.0.0.1:{_probePort} (attempt {attempt + 1})");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Port probe attempt {attempt + 1} failed: " + ex.Message);
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        if (!probeSuccess)
            Console.WriteLine($"Port probe failed: 127.0.0.1:{_probePort}");
    }

    Console.WriteLine("=== Calling app.WaitForShutdownAsync() ===");
    await app.WaitForShutdownAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine("=== FATAL: app startup threw an exception ===");
    Console.Error.WriteLine(ex.ToString());
    throw;
}
