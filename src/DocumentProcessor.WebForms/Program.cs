using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — bind the strongly-typed AppSettings from appsettings.json
// ---------------------------------------------------------------------------
builder.Services.Configure<AppSettings>(options =>
{
    builder.Configuration.GetSection("Database").Bind(options, o => o.ErrorOnUnknownConfiguration = false);
    builder.Configuration.GetSection("Bedrock").Bind(options, o => o.ErrorOnUnknownConfiguration = false);
    builder.Configuration.GetSection("Storage").Bind(options, o => o.ErrorOnUnknownConfiguration = false);

    // Map the structured section keys to the AppSettings property names.
    options.DatabaseUseSecretsManager = builder.Configuration.GetValue<bool>("Database:UseSecretsManager");
    options.DatabaseSecretDescriptionPrefix = builder.Configuration["Database:SecretDescriptionPrefix"]
        ?? options.DatabaseSecretDescriptionPrefix;
    options.BedrockRegion = builder.Configuration["Bedrock:Region"] ?? options.BedrockRegion;
    options.BedrockSummarizationModelId = builder.Configuration["Bedrock:SummarizationModelId"]
        ?? options.BedrockSummarizationModelId;
    options.BedrockMaxTokens = builder.Configuration.GetValue("Bedrock:MaxTokens", options.BedrockMaxTokens);
    options.BedrockMaxInputCharacters = builder.Configuration.GetValue("Bedrock:MaxInputCharacters",
        options.BedrockMaxInputCharacters);
    options.BedrockMaxPdfPages = builder.Configuration.GetValue("Bedrock:MaxPdfPages", options.BedrockMaxPdfPages);
    options.StorageRootPath = builder.Configuration["Storage:RootPath"] ?? options.StorageRootPath;
    options.StorageMaxFileSizeMegabytes = builder.Configuration.GetValue("Storage:MaxFileSizeMegabytes",
        options.StorageMaxFileSizeMegabytes);
    options.StorageMaxFilesPerUpload = builder.Configuration.GetValue("Storage:MaxFilesPerUpload",
        options.StorageMaxFilesPerUpload);
    options.StorageAllowedExtensionsRaw = builder.Configuration["Storage:AllowedExtensions"]
        ?? options.StorageAllowedExtensionsRaw;

    // The connection string lives under ConnectionStrings:DefaultConnection.
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "The 'DefaultConnection' connection string is missing from appsettings.json.");
});

// ---------------------------------------------------------------------------
// Database connection resolution (was Global.asax Application_Start)
// Resolves the connection string from config or AWS Secrets Manager, sets the
// static DocumentDbContext.ConnectionString, and registers DatabaseInfo as a
// singleton for the layout's DI injection.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DatabaseInfo>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var appSettings = sp.GetRequiredService<IOptions<AppSettings>>().Value;

    var connection = DatabaseConnectionResolver.Resolve(appSettings);

    if (connection.Warning != null)
    {
        logger.LogWarning("Database connection warning: {Warning}", connection.Warning);
    }

    // EF6's DocumentDbContext still uses a static connection string property.
    DocumentDbContext.ConnectionString = connection.ConnectionString;

    // Run schema initialization (was in Application_Start).
    try
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        DatabaseInitializer.EnsureSchema(connection.ConnectionString, env.ContentRootPath);
    }
    catch (Exception ex)
    {
        // Do not take the whole application down — the pages already report a
        // failure to read documents, and this way the error is visible in one place.
        logger.LogError(ex, "Could not prepare the database");
    }

    return connection.Info;
});

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Application services — DI registrations for the document processing pipeline
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();
builder.Services.AddScoped<DocumentDbContext>();

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Eagerly resolve DatabaseInfo so the connection string and schema are set
// before any request arrives (mirrors Application_Start running at boot).
// ---------------------------------------------------------------------------
_ = app.Services.GetRequiredService<DatabaseInfo>();

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
// Endpoints
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// [PORT-NOTE] ServicePointManager.SecurityProtocol TLS 1.2 pin dropped — .NET 10 uses
// the OS default TLS version which is already >= 1.2.

// [PORT-NOTE] Trace listeners dropped — replaced by ILogger<T> via ASP.NET Core's
// built-in logging configured in appsettings.json Logging section.

app.Run();
