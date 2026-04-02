// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ARCompletions.Config;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 綁定 Render 指派的 PORT（保險）
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

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
}

// 其他服務
builder.Services.AddControllersWithViews();
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
builder.Services.AddSingleton<ARCompletions.Services.IEmbeddingService, ARCompletions.Services.EmbeddingService>();
builder.Services.AddScoped<ARCompletions.Services.IEmbeddingRebuildService, ARCompletions.Services.EmbeddingRebuildService>();
// Drive service for file uploads (uses GOOGLE_SERVICE_ACCOUNT_KEY + GOOGLE_DRIVE_FOLDER_ID)
builder.Services.AddSingleton<ARCompletions.Services.IDriveService, ARCompletions.Services.GoogleDriveService>();

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

app.Logger.LogInformation("DB Provider: {Provider}", isPostgres ? "PostgreSQL" : "SQLite");
app.Logger.LogInformation("Auto-migrate on startup (RUN_MIGRATIONS): {Run}", runMigrations);
Console.WriteLine($"DB Provider: {(isPostgres ? "PostgreSQL" : "SQLite")}");
Console.WriteLine($"Auto-migrate on startup (RUN_MIGRATIONS): {runMigrations}");

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();

if (runMigrations)
{
    app.Logger.LogInformation("Applying EF Core migrations...");
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Migrations applied successfully.");
        Console.WriteLine("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply migrations on startup. Application will continue without applying migrations.");
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
    app.Logger.LogInformation("Ensuring database is created...");
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("EnsureCreated executed (database exists or was created).");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to ensure database creation on startup.");
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
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(headerKey) || !string.Equals(headerKey, expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, error = new { code = "Unauthorized", message = "Missing or invalid X-Internal-API-Key" } });
            return;
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
    app.Logger.LogWarning("Static image path not found: {Path}", imagePath);
    Console.WriteLine($"Static image path not found: {imagePath}");
}

app.UseHttpsRedirection();

// Area route for admin MVC
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
