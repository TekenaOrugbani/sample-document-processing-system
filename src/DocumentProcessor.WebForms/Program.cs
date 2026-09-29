using Amazon;
using Amazon.BedrockRuntime;
using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — bind the AppSettings POCO from appsettings.json
// ---------------------------------------------------------------------------
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

// Also bind flat keys that the legacy app stored at the root level of config.
// The AppSettings POCO expects structured names; map them here so IOptions<AppSettings>
// resolves every property regardless of which appsettings.json shape is active.
builder.Services.PostConfigure<AppSettings>(opts =>
{
    var cfg = builder.Configuration;

    if (string.IsNullOrEmpty(opts.ConnectionString))
    {
        opts.ConnectionString = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    opts.DatabaseUseSecretsManager = bool.TryParse(cfg["Database.UseSecretsManager"], out var useSm) && useSm;
    opts.DatabaseSecretDescriptionPrefix = cfg["Database.SecretDescriptionPrefix"]
                                           ?? opts.DatabaseSecretDescriptionPrefix;
    opts.BedrockRegion = cfg["Bedrock.Region"] ?? opts.BedrockRegion;
    opts.BedrockSummarizationModelId = cfg["Bedrock.SummarizationModelId"] ?? opts.BedrockSummarizationModelId;

    if (int.TryParse(cfg["Bedrock.MaxTokens"], out var mt))
        opts.BedrockMaxTokens = mt;
    if (int.TryParse(cfg["Bedrock.MaxInputCharacters"], out var mic))
        opts.BedrockMaxInputCharacters = mic;
    if (int.TryParse(cfg["Bedrock.MaxPdfPages"], out var mpp))
        opts.BedrockMaxPdfPages = mpp;

    opts.StorageRootPath = cfg["Storage.RootPath"] ?? opts.StorageRootPath;

    if (int.TryParse(cfg["Storage.MaxFileSizeMegabytes"], out var msf))
        opts.StorageMaxFileSizeMegabytes = msf;
    if (int.TryParse(cfg["Storage.MaxFilesPerUpload"], out var mfu))
        opts.StorageMaxFilesPerUpload = mfu;

    var extRaw = cfg["Storage.AllowedExtensions"];
    if (!string.IsNullOrEmpty(extRaw))
        opts.StorageAllowedExtensions = extRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
});

// ---------------------------------------------------------------------------
// Database — resolve connection string (with Secrets Manager fallback) and
// register the EF Core DbContext.
// ---------------------------------------------------------------------------
var earlySettings = new AppSettings();
builder.Configuration.GetSection("AppSettings").Bind(earlySettings);
// Also resolve flat keys the same way PostConfigure does later.
if (string.IsNullOrEmpty(earlySettings.ConnectionString))
{
    earlySettings.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
}
earlySettings.DatabaseUseSecretsManager = bool.TryParse(builder.Configuration["Database.UseSecretsManager"], out var useSmEarly) && useSmEarly;
earlySettings.DatabaseSecretDescriptionPrefix = builder.Configuration["Database.SecretDescriptionPrefix"]
                                                ?? earlySettings.DatabaseSecretDescriptionPrefix;

var connection = await DatabaseConnectionResolver.ResolveAsync(earlySettings);

if (connection.Warning is not null)
{
    // Use the host's built-in logger (the DI container is not built yet).
    Console.WriteLine($"[WARNING] {connection.Warning}");
}

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));
builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// Expose the resolved DatabaseInfo as a singleton so the MainLayout can
// display the connection metadata (engine, host, credential source).
builder.Services.AddSingleton(connection.Info);

// ---------------------------------------------------------------------------
// AWS Bedrock — register the runtime client as a singleton.
// The .NET 10 AWS SDK is async-only; the client itself is thread-safe.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IAmazonBedrockRuntime>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value;
    var config = new AmazonBedrockRuntimeConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(settings.BedrockRegion)
    };
    return new AmazonBedrockRuntimeClient(config);
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Request-size limits — the legacy app allowed 500 MB uploads
// (maxRequestLength / maxAllowedContentLength in Web.config).
// ---------------------------------------------------------------------------
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
});

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation (was DatabaseInitializer.EnsureSchema in
// Application_Start). Run once at startup; do not take the app down on
// failure — the pages already report read failures.
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.AppDataPath = Path.Combine(app.Environment.ContentRootPath, "App_Data");
    DatabaseInitializer.EnsureSchema(connection.ConnectionString);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Could not prepare the database");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see Microsoft docs).
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
