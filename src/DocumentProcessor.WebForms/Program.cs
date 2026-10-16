using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Kestrel limits — replaces httpRuntime maxRequestLength (512 MB in Web.config)
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    // 512 MB = 524_288_000 bytes (matches the old maxRequestLength=512000 KB)
    options.Limits.MaxRequestBodySize = 524_288_000;
});

// ---------------------------------------------------------------------------
// Request timeout — replaces httpRuntime executionTimeout (600 s in Web.config)
// ---------------------------------------------------------------------------
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(600)
    };
});

// ---------------------------------------------------------------------------
// Logging — replaces System.Diagnostics.Trace used throughout the legacy app
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ---------------------------------------------------------------------------
// Configuration — AppSettings wrapper registered as singleton for DI
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AppSettings>(sp =>
    new AppSettings(sp.GetRequiredService<IConfiguration>()));

// ---------------------------------------------------------------------------
// Database connection resolution & schema initialization
// ---------------------------------------------------------------------------
// DatabaseConnectionResolver.Resolve() may call AWS Secrets Manager, so run it
// early to obtain the connection string before registering the DbContext.
// The legacy Global.asax did the same in Application_Start.
// PORT-TODO
// {"was": "DocumentProcessor.WebForms.Data.DatabaseConnectionResolver", "note": "DatabaseConnectionResolver still uses static AppSettings.ConnectionString / AppSettings.DatabaseUseSecretsManager. Once AppSettings is no longer static, DatabaseConnectionResolver must accept AppSettings via a parameter or be registered in DI. For now, AppSettings is instantiated manually for the startup path.", "files": ["Data/DatabaseConnectionResolver.cs"]}
var startupSettings = new AppSettings(builder.Configuration);
var connection = DatabaseConnectionResolver.Resolve(startupSettings);

if (connection.Warning is not null)
{
    // Log the warning once the app is built; for now store it.
    Console.WriteLine($"[WARN] {connection.Warning}");
}

// Make DatabaseInfo available as a singleton so the MainLayout can @inject it.
builder.Services.AddSingleton<DatabaseInfo>(connection.Info);

// ---------------------------------------------------------------------------
// EF Core DbContext — replaces EF6 static ConnectionString + Database.SetInitializer
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();

builder.Services.AddSingleton<IDocumentSummarizer, BedrockDocumentSummarizer>();

builder.Services.AddSingleton<DocumentTextExtractor>();

builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Schema initialization — replaces DatabaseInitializer.EnsureSchema in
// Application_Start. Runs the App_Data/Schema.sql script against SQL Server.
// ---------------------------------------------------------------------------
try
{
    var scriptPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "Schema.sql");
    if (File.Exists(scriptPath))
    {
        var script = File.ReadAllText(scriptPath);
        var connBuilder = new SqlConnectionStringBuilder(connection.ConnectionString);
        var databaseName = connBuilder.InitialCatalog;

        // Create the database if it does not exist (mirrors DatabaseInitializer logic).
        if (!string.IsNullOrEmpty(databaseName))
        {
            var masterBuilder = new SqlConnectionStringBuilder(connBuilder.ConnectionString)
            {
                InitialCatalog = "master"
            };

            const string createDbSql =
                "IF DB_ID(@name) IS NULL " +
                "BEGIN " +
                "  DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); " +
                "  EXEC sp_executesql @sql; " +
                "END";

            using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
            using var createCmd = new SqlCommand(createDbSql, masterConn);
            createCmd.Parameters.AddWithValue("@name", databaseName);
            masterConn.Open();
            createCmd.ExecuteNonQuery();
        }

        // Run the schema script.
        using var conn = new SqlConnection(connection.ConnectionString);
        using var cmd = new SqlCommand(script, conn);
        conn.Open();
        cmd.ExecuteNonQuery();
    }
    else
    {
        app.Logger.LogWarning("Schema script not found at {Path}; skipping database initialization.", scriptPath);
    }
}
catch (Exception ex)
{
    // Mirror the legacy behaviour: do not take the whole application down.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// Log the connection warning that was captured before the logger existed.
if (connection.Warning is not null)
{
    app.Logger.LogWarning("{Warning}", connection.Warning);
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see ASP.NET Core middleware order)
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

// Status-code pages — replaces customErrors in Web.config
app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestTimeouts();
app.UseAntiforgery();

// ---------------------------------------------------------------------------
// Root route redirect — Default.aspx was the default document; ensure "/" resolves
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Blazor Server entry — modern .NET 8+ pattern (NOT MapBlazorHub / _Host)
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
