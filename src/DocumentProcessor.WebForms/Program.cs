using Amazon;
using Amazon.BedrockRuntime;
using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration: bind the AppSettings POCO from appsettings.json sections
// ---------------------------------------------------------------------------
builder.Services.Configure<AppSettings>(options =>
{
    var config = builder.Configuration;
    options.ConnectionString = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "The 'DefaultConnection' connection string is missing from appsettings.json.");
    options.DatabaseUseSecretsManager = config.GetValue<bool>("Database:UseSecretsManager");
    options.DatabaseSecretDescriptionPrefix = config.GetValue<string>("Database:SecretDescriptionPrefix")
        ?? "Password for RDS MSSQL used for MAM417.";
    options.BedrockRegion = config.GetValue<string>("Bedrock:Region") ?? "us-east-1";
    options.BedrockSummarizationModelId = config.GetValue<string>("Bedrock:SummarizationModelId")
        ?? "global.anthropic.claude-sonnet-5";
    options.BedrockMaxTokens = config.GetValue<int>("Bedrock:MaxTokens", 2000);
    options.BedrockMaxInputCharacters = config.GetValue<int>("Bedrock:MaxInputCharacters", 10000);
    options.BedrockMaxPdfPages = config.GetValue<int>("Bedrock:MaxPdfPages", 5);
    options.StorageRootPath = config.GetValue<string>("Storage:RootPath") ?? "uploads";
    options.StorageMaxFileSizeMegabytes = config.GetValue<int>("Storage:MaxFileSizeMegabytes", 50);
    options.StorageMaxFilesPerUpload = config.GetValue<int>("Storage:MaxFilesPerUpload", 10);
    options.StorageAllowedExtensionsRaw = config.GetValue<string>("Storage:AllowedExtensions") ?? ".pdf,.txt,.log";
});

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Database: resolve connection, register EF Core DbContext
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "The 'DefaultConnection' connection string is missing from appsettings.json.");

// The legacy app optionally resolved the connection string from AWS Secrets Manager.
// DatabaseConnectionResolver.Resolve() encapsulates that fallback logic.
var useSecretsManager = builder.Configuration.GetValue<bool>("Database:UseSecretsManager");
var secretPrefix = builder.Configuration.GetValue<string>("Database:SecretDescriptionPrefix")
    ?? "Password for RDS MSSQL used for MAM417.";
var databaseConnection = DatabaseConnectionResolver.Resolve(connectionString, useSecretsManager, secretPrefix);
connectionString = databaseConnection.ConnectionString;

if (databaseConnection.Warning is not null)
{
    // The logger is not yet built; use Console as Application_Start used Trace.
    Console.WriteLine($"[WARNING] {databaseConnection.Warning}");
}

// Register DatabaseInfo as a singleton so the MainLayout and other components
// can @inject it (replaces Application[DatabaseInfoKey]).
builder.Services.AddSingleton(databaseConnection.Info);

// EF Core DbContext — uses the resolved connection string.
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------------------------------------------------------------------------
// Application services (replaces direct instantiation in DocumentPipeline etc.)
// ---------------------------------------------------------------------------

// IAmazonBedrockRuntime — singleton; AWS SDK clients are thread-safe and expensive to create.
builder.Services.AddSingleton<IAmazonBedrockRuntime>(sp =>
{
    var appSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value;
    var config = new AmazonBedrockRuntimeConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(appSettings.BedrockRegion)
    };
    return new AmazonBedrockRuntimeClient(config);
});

// Storage — scoped so each request gets the correct IWebHostEnvironment paths.
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();

// Summarizer — scoped; uses the singleton IAmazonBedrockRuntime client internally.
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();

// Text extraction — stateless, transient is fine.
builder.Services.AddTransient<DocumentTextExtractor>();

// Processing pipeline — scoped (needs DbContext and the other scoped services).
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Kestrel limits (migrated from IIS maxRequestLength / requestFiltering)
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var maxBody = builder.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize");
    if (maxBody.HasValue)
    {
        serverOptions.Limits.MaxRequestBodySize = maxBody.Value;
    }
});

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialization (carried over from Application_Start)
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.AppDataPath = Path.Combine(app.Environment.ContentRootPath, "App_Data");
    DatabaseInitializer.EnsureSchema(connectionString);
}
catch (Exception ex)
{
    // Match legacy behaviour: log but do not crash the application.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see Microsoft middleware-order docs)
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
// Application_Error → centralised exception logging
// ---------------------------------------------------------------------------
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
        throw; // re-throw so UseExceptionHandler can handle it
    }
});

// ---------------------------------------------------------------------------
// Blazor Server endpoints
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
