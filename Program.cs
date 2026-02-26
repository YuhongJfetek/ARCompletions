// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ARCompletions.Config;

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
else
{
    // ---- SQLite 後備（本機或未設定 DATABASE_URL 時）----
    string dbPath;
    var envDbPath = Environment.GetEnvironmentVariable("DB_PATH");
    if (!string.IsNullOrWhiteSpace(envDbPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(envDbPath)!);
        dbPath = envDbPath!;
    }
    else
    {
        var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        dbPath = Path.Combine(dataDir, "ARCompletions.db");
    }

    builder.Services.AddDbContext<ARCompletionsContext>(opt =>
        opt.UseSqlite($"Data Source={dbPath}")); // ← 已移除 .MigrationsAssembly(...)
}

// 其他服務
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// bind embedding config (can be set via appsettings or secrets / env vars)
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
// typed HttpClient for OpenAI requests (used by AnalysisWorker)
builder.Services.AddHttpClient("OpenAI", c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
// embedding service
builder.Services.AddSingleton<ARCompletions.Services.IEmbeddingService, ARCompletions.Services.EmbeddingService>();
// Authentication for admin area (cookie-based, simple dev helper)
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Admin/Home/Login";
        options.Cookie.HttpOnly = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddHostedService<ARCompletions.Services.AnalysisWorker>();

var app = builder.Build();

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

if (runMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
    try
    {
        app.Logger.LogInformation("Applying EF Core migrations...");
        db.Database.Migrate(); // 只套用遷移（結構變更）；不動既有資料
        app.Logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database.Migrate() failed. Not falling back to EnsureCreated().");
        throw;
    }
}
else
{
    app.Logger.LogInformation("Skipping EF migrations on startup (RUN_MIGRATIONS=false)");
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
}

app.UseHttpsRedirection();

// Area route for admin MVC
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
