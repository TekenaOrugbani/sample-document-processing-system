using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration: bind strongly-typed option sections from appsettings.json
// ---------------------------------------------------------------------------
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection(DatabaseSettings.SectionName));
builder.Services.Configure<BedrockSettings>(
    builder.Configuration.GetSection(BedrockSettings.SectionName));
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));

// AppSettings wraps IConfiguration for callers that read cross-section values.
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// Database: resolve connection at startup (may come from AWS Secrets Manager)
// ---------------------------------------------------------------------------
// DatabaseConnectionResolver.Resolve() reads AppSettings, which now requires
// IConfiguration. Build a temporary AppSettings to resolve the connection.
var appSettings = new AppSettings(builder.Configuration);
var connection = DatabaseConnectionResolver.Resolve(appSettings);

if (connection.Warning is not null)
{
    // Cannot use ILogger yet (host not built); write to console as a fallback.
    Console.WriteLine("[warn] " + connection.Warning);
}

// Register DatabaseInfo as a singleton so Components (MainLayout) can inject it.
builder.Services.AddSingleton(connection.Info);

// Register EF Core DbContext with the resolved connection string.
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// Legacy DocumentDbContextFactory — still used by DocumentPipeline and other
// callers that create short-lived DbContext instances per unit of work.
builder.Services.AddSingleton(new DocumentDbContextFactory(connection.ConnectionString));

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Upload size limits (migrated from Web.config maxRequestLength / requestLimits)
// ---------------------------------------------------------------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // ~500 MB
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524_288_000;
});

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation (was Application_Start → DatabaseInitializer)
// ---------------------------------------------------------------------------
try
{
    var env = app.Services.GetRequiredService<IWebHostEnvironment>();
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, env);
}
catch (Exception ex)
{
    // Mirror legacy behaviour: log but do not crash the host on schema errors.
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    startupLogger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing; see ASP.NET Core middleware docs
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Centralised exception logging (replaces Application_Error).
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled application error.");
        throw; // re-throw so UseExceptionHandler can handle the response
    }
});

// Custom error pages (migrated from Web.config customErrors)
app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------
// Root-route redirect: Default.aspx was the IIS default document → redirect
// "/" to the ported landing page once it exists. If a ported page already owns
// @page "/", this redirect is harmless (the page takes priority).
app.MapGet("/", () => Results.Redirect("/Default"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
