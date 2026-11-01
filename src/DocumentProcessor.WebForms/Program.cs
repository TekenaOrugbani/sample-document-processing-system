using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration: wire AppSettings so existing static accessors keep working.
// The PORT-TODO in AppSettings.cs requested this call.
// ---------------------------------------------------------------------------
AppSettings.Configure(builder.Configuration);

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Database: resolve the connection string (Web.config/Secrets Manager logic)
// and register EF Core with SQL Server.
// ---------------------------------------------------------------------------
var dbConnection = await DatabaseConnectionResolver.ResolveAsync();

if (dbConnection.Warning is not null)
{
    // The legacy Application_Start logged this via Trace; use the host logger instead.
    var startupLogger = LoggerFactory
        .Create(lb => lb.AddConfiguration(builder.Configuration.GetSection("Logging")).AddConsole())
        .CreateLogger("Startup");
    startupLogger.LogWarning("{Warning}", dbConnection.Warning);
}

builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(dbConnection.ConnectionString));

// Expose the resolved DatabaseInfo as a singleton so MainLayout can @inject it.
builder.Services.AddSingleton(dbConnection.Info);

// ---------------------------------------------------------------------------
// Application services (replacing static/new-up patterns in code-behind)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storagePath = AppSettings.StorageRootPath;

    // The legacy virtual path ~/App_Data/uploads was resolved by Server.MapPath.
    // appsettings.json now stores a relative path ("uploads"); resolve against ContentRootPath.
    var rootPath = Path.IsPathRooted(storagePath)
        ? storagePath
        : Path.Combine(env.ContentRootPath, storagePath);

    return new FileSystemDocumentStorage(rootPath);
});

builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Logging (replaces System.Diagnostics.Trace)
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ---------------------------------------------------------------------------
// Build the application
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialization (legacy Application_Start → startup)
// ---------------------------------------------------------------------------
try
{
    var schemaScriptPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "Schema.sql");
    DatabaseInitializer.EnsureSchema(
        dbConnection.ConnectionString,
        File.Exists(schemaScriptPath) ? schemaScriptPath : null);
}
catch (Exception ex)
{
    // The legacy Application_Start caught this and logged it without taking the
    // whole application down; preserve that behavior.
    app.Logger.LogError(ex, "Could not prepare the database");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    // Legacy customErrors mode="RemoteOnly" → exception handler + status code pages.
    app.UseExceptionHandler("/ErrorPage");
    app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Modern Blazor Server entry (.NET 8+ pattern)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
