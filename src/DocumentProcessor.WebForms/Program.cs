using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration – bind the AppSettings options class from the separate JSON
// sections (Database, Bedrock, Storage) that port-webconfig produced.
// ---------------------------------------------------------------------------
builder.Services.Configure<AppSettings>(options =>
{
    var config = builder.Configuration;

    // Connection string
    options.ConnectionString = config.GetConnectionString("DefaultConnection") ?? string.Empty;

    // Database section
    var db = config.GetSection("Database");
    if (bool.TryParse(db["UseSecretsManager"], out var useSm))
        options.DatabaseUseSecretsManager = useSm;
    options.DatabaseSecretDescriptionPrefix =
        db["SecretDescriptionPrefix"] ?? options.DatabaseSecretDescriptionPrefix;

    // Bedrock section
    var bedrock = config.GetSection("Bedrock");
    options.BedrockRegion = bedrock["Region"] ?? options.BedrockRegion;
    options.BedrockSummarizationModelId =
        bedrock["SummarizationModelId"] ?? options.BedrockSummarizationModelId;
    if (int.TryParse(bedrock["MaxTokens"], out var maxTokens))
        options.BedrockMaxTokens = maxTokens;
    if (int.TryParse(bedrock["MaxInputCharacters"], out var maxInput))
        options.BedrockMaxInputCharacters = maxInput;
    if (int.TryParse(bedrock["MaxPdfPages"], out var maxPages))
        options.BedrockMaxPdfPages = maxPages;

    // Storage section
    var storage = config.GetSection("Storage");
    options.StorageRootPath = storage["RootPath"] ?? options.StorageRootPath;
    if (int.TryParse(storage["MaxFileSizeMegabytes"], out var maxSize))
        options.StorageMaxFileSizeMegabytes = maxSize;
    if (int.TryParse(storage["MaxFilesPerUpload"], out var maxFiles))
        options.StorageMaxFilesPerUpload = maxFiles;
    options.StorageAllowedExtensionsRaw =
        storage["AllowedExtensions"] ?? options.StorageAllowedExtensionsRaw;
});

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Database – resolve connection at startup (optionally from AWS Secrets Manager)
// and register the EF Core DbContext and a singleton DatabaseInfo.
// ---------------------------------------------------------------------------
var configuredConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
var useSecretsManager =
    bool.TryParse(builder.Configuration["Database:UseSecretsManager"], out var sm) && sm;
var secretPrefix =
    builder.Configuration["Database:SecretDescriptionPrefix"]
    ?? "Password for RDS MSSQL used for MAM417.";

var connection = DatabaseConnectionResolver.Resolve(
    configuredConnectionString, useSecretsManager, secretPrefix);

if (connection.Warning != null)
{
    // Application_Start logged this via Trace; use the built-in logger instead.
    var startupLogger = LoggerFactory
        .Create(lb => lb.AddConsole())
        .CreateLogger("Startup");
    startupLogger.LogWarning("{Warning}", connection.Warning);
}

// Register DatabaseInfo as a singleton so components can @inject it (MainLayout does).
builder.Services.AddSingleton(connection.Info);

// EF Core DbContext – the Phase 2 migration already gave DocumentDbContext a
// DbContextOptions constructor. Register it with the resolved connection string.
// AddDbContextFactory is used because Blazor Server components create/dispose
// their own DbContext instances via IDbContextFactory to avoid concurrency issues.
builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// ---------------------------------------------------------------------------
// Application services – previously instantiated with `new` in code-behind;
// now registered in DI so Blazor components can inject them.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value;
    var rootPath = Path.Combine(env.ContentRootPath, settings.StorageRootPath);
    return new FileSystemDocumentStorage(rootPath);
});

builder.Services.AddScoped<DocumentTextExtractor>();

builder.Services.AddSingleton<IDocumentSummarizer, BedrockDocumentSummarizer>();

builder.Services.AddScoped<DocumentPipeline>();

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation – replaces the Application_Start call to
// DatabaseInitializer.EnsureSchema. Runs once at startup.
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.EnsureSchema(
        connection.ConnectionString,
        app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    // Original Global.asax logged the error but did NOT take the app down.
    app.Logger.LogError(ex, "Could not prepare the database");
}

// ---------------------------------------------------------------------------
// Middleware pipeline – order is load-bearing.
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
// Blazor Server endpoint – modern .NET 8+ pattern
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
