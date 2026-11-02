using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Configuration: AppSettings wrapper over IConfiguration (already ported from
// static ConfigurationManager access in Phase 2). Registered as a singleton
// so every service that previously read static AppSettings.* can inject it.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// DatabaseConnection singleton — resolved via DatabaseConnectionResolver.Resolve().
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DatabaseConnection>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    var resolver = new DatabaseConnectionResolver(appSettings);
    return resolver.Resolve();
});

// ---------------------------------------------------------------------------
// Database — EF Core DbContext (Phase 2 already migrated DocumentDbContext to
// EF Core with constructor-injected DbContextOptions and a global query filter
// for soft-delete). Replaces the legacy static DocumentDbContext.ConnectionString
// and inline `new DocumentDbContext()` pattern.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<DocumentDbContext>((sp, options) =>
{
    var dbConnection = sp.GetRequiredService<DatabaseConnection>();

    // PORT-TODO
    // {"note": "When Database.UseSecretsManager is true in appsettings.json the connection string should be resolved from AWS Secrets Manager via DatabaseConnectionResolver. For now the DbContext uses the appsettings.json connection string directly. Wire DatabaseConnectionResolver.Resolve() into the connection-string resolution once that class is ported to accept AppSettings via DI.", "files": ["Data/DatabaseConnectionResolver.cs"]}

    options.UseSqlServer(dbConnection.ConnectionString);
});

// ---------------------------------------------------------------------------
// Application services — previously instantiated inline with `new` everywhere.
// Now registered in the DI container so Blazor components can @inject them.
// ---------------------------------------------------------------------------

// IDocumentStorage ← FileSystemDocumentStorage
// PORT-TODO
// {"note": "FileSystemDocumentStorage constructor currently calls HostingEnvironment.MapPath(AppSettings.StorageRootPath) — it must be ported to accept (string rootPath) or (AppSettings, IWebHostEnvironment) via DI. The registration below uses a factory that resolves the storage root relative to ContentRootPath. Update the FileSystemDocumentStorage constructor to accept a string rootPath parameter.", "was": "System.Web.Hosting.HostingEnvironment.MapPath", "files": ["Services/FileSystemDocumentStorage.cs"]}
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();

    // The legacy code used HostingEnvironment.MapPath("~/App_Data/uploads").
    // On ASP.NET Core the storage root is resolved relative to ContentRootPath.
    var storagePath = appSettings.StorageRootPath;
    if (storagePath.StartsWith("~/"))
    {
        storagePath = storagePath.Substring(2);
    }

    var fullPath = Path.Combine(env.ContentRootPath, storagePath);
    return new FileSystemDocumentStorage(fullPath);
});

// DocumentTextExtractor — stateless text extraction from PDF/TXT/LOG files.
// PORT-TODO
// {"note": "DocumentTextExtractor currently reads AppSettings.BedrockMaxPdfPages and AppSettings.BedrockMaxInputCharacters via static access. It must be ported to accept AppSettings via constructor injection. The registration below uses DI auto-resolution — update the constructor to accept AppSettings.", "files": ["Services/DocumentTextExtractor.cs"]}
builder.Services.AddScoped<DocumentTextExtractor>();

// IDocumentSummarizer ← BedrockDocumentSummarizer
// PORT-TODO
// {"note": "BedrockDocumentSummarizer uses a static IAmazonBedrockRuntime Client created from static AppSettings and calls Client.Converse() synchronously. It must be ported to: (1) accept AppSettings via constructor injection, (2) use ConverseAsync() instead of the synchronous Converse() to avoid blocking the Blazor Server circuit, (3) change IDocumentSummarizer.Summarize to return Task<string>. Async propagation must flow through DocumentPipeline.Process as well.", "was": "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient.Converse", "files": ["Services/BedrockDocumentSummarizer.cs", "Services/IDocumentSummarizer.cs", "Services/DocumentPipeline.cs"]}
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();

// DocumentPipeline — orchestrates extract → summarize → persist.
builder.Services.AddScoped<DocumentPipeline>();

// DatabaseConnectionResolver — resolves connection string from appsettings.json or
// AWS Secrets Manager. Used at startup for DatabaseInfo and optionally for DbContext.
// PORT-TODO
// {"note": "DatabaseConnectionResolver.Resolve() currently uses static AppSettings properties. It must be ported to accept AppSettings as a parameter or via DI. The static methods DetectProvider and HostOf should be made public for use from Program.cs or refactored.", "files": ["Data/DatabaseConnectionResolver.cs"]}

// HttpContextAccessor — needed if any downstream code accesses HttpContext.
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// DatabaseInfo singleton — surfaces the resolved connection metadata in the UI
// (provider, credential source, host address). Replaces the legacy
// HttpApplication.Application[DatabaseInfoKey] application-level state.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DatabaseInfo>(sp => sp.GetRequiredService<DatabaseConnection>().Info);

// ---------------------------------------------------------------------------
// Kestrel: allow 512 MB request bodies for large file uploads.
// Replaces httpRuntime maxRequestLength="524288" from Web.config.
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 512L * 1024 * 1024;
});

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Log DatabaseConnection warnings (e.g. Secrets Manager fallback).
// ---------------------------------------------------------------------------
{
    var dbConnection = app.Services.GetRequiredService<DatabaseConnection>();
    if (dbConnection.Warning != null)
    {
        app.Logger.LogWarning(dbConnection.Warning);
    }
}

// ---------------------------------------------------------------------------
// Database schema initialization — replaces DatabaseInitializer.EnsureSchema()
// that ran in Application_Start. Uses EF Core's EnsureCreated() to create the
// tables from the model when the database exists but the tables do not.
// ---------------------------------------------------------------------------
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    db.Database.EnsureCreated();
}
catch (Exception ex)
{
    // Match the legacy behavior: log but do not take the whole application down.
    // Pages already report a failure to read documents.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see ASP.NET Core middleware order).
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files served from wwwroot/ — replaces IIS static file handling.
app.UseStaticFiles();

app.UseRouting();

// [PORT-NOTE] TLS 1.2 pinning (ServicePointManager.SecurityProtocol |= Tls12) dropped:
// not needed on modern .NET — the runtime negotiates TLS 1.2+ by default.

// [PORT-NOTE] Application_Error handler replaced by UseExceptionHandler middleware
// above; unhandled exceptions are logged by the framework logging pipeline.

// [PORT-NOTE] No authentication/authorization configured — the original app had no
// forms auth. UseAuthentication/UseAuthorization omitted intentionally.

app.UseAntiforgery();

// Status code pages — redirect 404 etc. to the ErrorPage component, replacing
// customErrors from Web.config.
app.UseStatusCodePagesWithRedirects("/ErrorPage?code={0}");

// ---------------------------------------------------------------------------
// Blazor Server endpoint — modern .NET 8+ pattern using MapRazorComponents<App>.
// NOT MapBlazorHub + MapFallbackToPage("/_Host") (the .NET 6/7 pattern).
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
