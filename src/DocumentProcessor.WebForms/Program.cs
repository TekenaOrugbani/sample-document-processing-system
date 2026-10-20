using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.SecretsManager;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — AppSettings static accessor
// ---------------------------------------------------------------------------
// AppSettings.cs reads from IConfiguration; initialize it before any setting
// is accessed. (The class already migrated to IConfiguration in Phase 2.)
AppSettings.Initialize(builder.Configuration);

// ---------------------------------------------------------------------------
// Kestrel limits — match the legacy web.config httpRuntime / requestFiltering
// maxRequestLength=512000 KB ≈ 500 MB, executionTimeout=600 s
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(600);
});

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Database — EF Core (already migrated from EF6 in Phase 2)
// ---------------------------------------------------------------------------
// Resolve the connection through the same logic that Application_Start used
// (supports AWS Secrets Manager fallback).
var connection = DatabaseConnectionResolver.Resolve();

if (connection.Warning != null)
{
    // Log to the built-in logger (replaces Trace.TraceWarning).
    var startupLogger = LoggerFactory
        .Create(lb => lb.AddConsole())
        .CreateLogger("Startup");
    startupLogger.LogWarning("{Warning}", connection.Warning);
}

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// ---------------------------------------------------------------------------
// DatabaseInfo singleton — replaces Application["DatabaseInfo"] from Global.asax
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(connection.Info);

// ---------------------------------------------------------------------------
// AWS SDK clients (v4, thread-safe singletons)
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ =>
{
    var config = new AmazonBedrockRuntimeConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(AppSettings.BedrockRegion)
    };
    return new AmazonBedrockRuntimeClient(config);
});

builder.Services.AddSingleton<IAmazonSecretsManager>(_ =>
    new AmazonSecretsManagerClient());

// ---------------------------------------------------------------------------
// Application services — previously newed inline, now DI-registered
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storageSetting = AppSettings.StorageRootPath;
    // The legacy setting was "~/App_Data/uploads"; resolve against ContentRootPath.
    var rootPath = Path.IsPathRooted(storageSetting)
        ? storageSetting
        : Path.Combine(env.ContentRootPath, storageSetting.TrimStart('~').TrimStart('/'));
    return new FileSystemDocumentStorage(rootPath);
});

builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Logging
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation (replaces Global.asax Application_Start)
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    // Do not take the whole application down — the pages already report a
    // failure to read documents, and this way the error is visible in one place.
    app.Logger.LogError(ex, "Could not prepare the database");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing
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

// ---------------------------------------------------------------------------
// Blazor Server endpoint — modern .NET 8+ pattern
// (NOT MapBlazorHub + MapFallbackToPage("/_Host"))
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
