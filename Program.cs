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
builder.Services.AddSwaggerGen();
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
// Line bot service
builder.Services.AddScoped<ARCompletions.Services.LineBotService>();
// Line business service (persistence + enqueue)
builder.Services.AddScoped<ARCompletions.Services.ILineService, ARCompletions.Services.LineService>();
// embedding service
builder.Services.AddSingleton<ARCompletions.Services.IEmbeddingService, ARCompletions.Services.EmbeddingService>();
// Authentication removed: admin login/logout UI removed; handle auth externally if needed

builder.Services.AddHostedService<ARCompletions.Services.AnalysisWorker>();

var app = builder.Build();

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

// Authentication middleware removed because login/logout endpoints were removed

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
