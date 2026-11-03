using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — bind strongly-typed options
// ---------------------------------------------------------------------------
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<BedrockOptions>(
    builder.Configuration.GetSection(BedrockOptions.SectionName));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// Initialize the static AppSettings facade so existing callers
// (DatabaseConnectionResolver, AppSettings.ConnectionString, etc.)
// continue to work until they are fully migrated to IOptions<T>.
AppSettings.Initialize(builder.Configuration);

// ---------------------------------------------------------------------------
// Database — resolve connection string (may call AWS Secrets Manager)
// ---------------------------------------------------------------------------
// The legacy Application_Start resolved the connection string once at boot;
// replicate that here so EF Core has a provider-configured DbContext and the
// MainLayout can display the connection info banner.
DatabaseConnection databaseConnection;
try
{
    databaseConnection = await DatabaseConnectionResolver.ResolveAsync();
}
catch (Exception ex)
{
    // Fall back to the configured connection string so the app can still
    // start and surface the error through the UI / logs.
    var fallback = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is missing and Secrets Manager resolution also failed.", ex);
    databaseConnection = new DatabaseConnection(
        fallback,
        new DatabaseInfo(DatabaseConnectionResolver.DetectProvider(fallback), "appsettings.json", "unknown"),
        "Secrets Manager lookup failed; using the configured connection string.");
}

if (databaseConnection.Warning != null)
{
    // The logger is not built yet, so write to the console directly.
    Console.WriteLine("WARNING: " + databaseConnection.Warning);
}

// Register DatabaseInfo as a singleton so MainLayout can @inject it.
builder.Services.AddSingleton(databaseConnection.Info);

// EF Core — replaces the static ConnectionString pattern and Entity Framework 6.
builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(databaseConnection.ConnectionString));

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Kestrel limits — ported from Web.config
// httpRuntime maxRequestLength="512000" (KB) → 512 MB
// system.webServer requestLimits maxAllowedContentLength="524288000" (bytes) → ~500 MB
// httpRuntime executionTimeout="600" → 600 seconds
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524_288_000;
});
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy =
        new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(600)
        };
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation — ported from Application_Start
// ---------------------------------------------------------------------------
DatabaseInitializer.ContentRootPath = app.Environment.ContentRootPath;
try
{
    DatabaseInitializer.EnsureSchema(databaseConnection.ConnectionString);
}
catch (Exception ex)
{
    // Do not take the whole application down: the pages already report a
    // failure to read documents, and this way the error is visible in one place.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order matters
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestTimeouts();
app.UseAntiforgery();

// Application_Error → centralised exception logging.
// In development, the developer exception page already handles this.
// In production, UseExceptionHandler above redirects to /ErrorPage.
// Additionally log every unhandled exception so the behaviour matches
// the original Application_Error handler.
if (app.Environment.IsDevelopment())
{
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
            throw; // Re-throw so the developer exception page can render.
        }
    });
}

app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");


app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
