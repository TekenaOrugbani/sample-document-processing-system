using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Initialize the static AppSettings bridge so that legacy service classes
// (DatabaseConnectionResolver, FileSystemDocumentStorage, etc.) can still
// read configuration through AppSettings.* until they are migrated to
// IOptions<T> / IConfiguration injection.
// ---------------------------------------------------------------------------
AppSettings.Initialize(builder.Configuration);

// ---------------------------------------------------------------------------
// Kestrel / form upload limits
// Legacy Web.config had maxRequestLength = 512 000 KB (~500 MB) and
// maxAllowedContentLength = 524 288 000 bytes.  Re-express as Kestrel
// MaxRequestBodySize and FormOptions.MultipartBodyLengthLimit.
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524_288_000;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000;
});

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Database — resolve the connection string (may come from Secrets Manager)
// and make it available via DI. The static DocumentDbContext.ConnectionString
// is set as a bridge for the EF6 DbContext until it is replaced with EF Core
// AddDbContext.
// ---------------------------------------------------------------------------
var connection = DatabaseConnectionResolver.Resolve();

if (connection.Warning != null)
{
    // Startup warnings go to the host logger once it is available;
    // Console.Error is the only sink this early.
    Console.Error.WriteLine("[WARNING] " + connection.Warning);
}

// Bridge: EF6 DbContext still reads a static property.
DocumentDbContext.ConnectionString = connection.ConnectionString;

// Register DocumentDbContext for DI — the Blazor pages inject it.
builder.Services.AddScoped<DocumentDbContext>();

// PORT-TODO
// {"note": "Replace static DocumentDbContext.ConnectionString with EF Core AddDbContext<DocumentDbContext>(o => o.UseSqlServer(...)) once the DbContext is migrated from EF6 to EF Core."}

// ---------------------------------------------------------------------------
// IDatabaseInfoProvider — singleton that replaces
// HttpContext.Current.Application[DatabaseInfoKey]
// ---------------------------------------------------------------------------
var databaseInfoProvider = new DatabaseInfoProvider { Info = connection.Info };
builder.Services.AddSingleton<IDatabaseInfoProvider>(databaseInfoProvider);

// ---------------------------------------------------------------------------
// Application services — previously instantiated with 'new' inside pages
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Infrastructure
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Logging (replaces the Trace.Listeners TextWriterTraceListener in
// Global.asax — ASP.NET Core's built-in logging subsystem supersedes it)
// ---------------------------------------------------------------------------
builder.Logging.AddConsole();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation — mirrors the Application_Start try/catch
// that ran DatabaseInitializer.EnsureSchema.
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.ContentRootPath = app.Environment.ContentRootPath;
    DatabaseInitializer.EnsureSchema(connection.ConnectionString);
}
catch (Exception ex)
{
    // Do not take the whole application down: the pages already report a
    // failure to read documents, and this way the error is visible in the logs.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// ServicePointManager.SecurityProtocol TLS pinning is removed.
// .NET 10.0 negotiates TLS 1.2+ by default; explicit pinning is unnecessary
// and can prevent negotiation of newer protocol versions.
// [PORT-NOTE] Dropped ServicePointManager.SecurityProtocol |= Tls12 pinning:
// .NET 10.0 handles TLS negotiation automatically.
// ---------------------------------------------------------------------------

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
// Endpoint mapping — Blazor Server (.NET 8+ pattern)
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
