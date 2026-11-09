using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;

var builder = WebApplication.CreateBuilder(args);

// Wire the static AppConfiguration accessor so that AppSettings (and every class
// that reads configuration through it) continues to work without rewriting every
// call site in this pass.
AppConfiguration.Configure(builder.Configuration);

// ---------------------------------------------------------------------------
// Blazor Server services
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Database — resolve connection (optionally from Secrets Manager) at startup
// ---------------------------------------------------------------------------
var connection = DatabaseConnectionResolver.Resolve();

if (connection.Warning != null)
{
    var bootstrapLogger = LoggerFactory.Create(lb => lb.AddConsole())
        .CreateLogger("Startup");
    bootstrapLogger.LogWarning("{Warning}", connection.Warning);
}

// Make DatabaseInfo available to any component that needs to display connection
// metadata (replaces the legacy Application[DatabaseInfoKey] slot).
builder.Services.AddSingleton(connection.Info);

// Register DocumentDbContext for DI.  The legacy app used a static ConnectionString
// property set from Global.asax; the modern approach is AddDbContext<> with the
// resolved connection string.  The project still uses EF6 (EntityFramework package),
// whose DbContext does not accept DbContextOptions the way EF Core does, so we keep
// the static property assignment and register a transient factory that creates the
// context the same way the legacy code did.
//
DocumentDbContext.ConnectionString = connection.ConnectionString;
builder.Services.AddTransient<DocumentDbContext>();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IDocumentStorage>(sp =>
{
    // The legacy FileSystemDocumentStorage used HostingEnvironment.MapPath to
    // resolve the virtual "~/App_Data/uploads" path.  Map it to a physical path
    // under the content root.
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storageSetting = AppSettings.StorageRootPath;

    string rootPath;
    if (storageSetting.StartsWith("~/"))
    {
        rootPath = Path.Combine(env.ContentRootPath, storageSetting.Substring(2).Replace('/', Path.DirectorySeparatorChar));
    }
    else
    {
        rootPath = Path.Combine(env.ContentRootPath, storageSetting.Replace('/', Path.DirectorySeparatorChar));
    }

    return new FileSystemDocumentStorage(rootPath);
});

builder.Services.AddSingleton<DocumentTextExtractor>();
builder.Services.AddSingleton<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Database schema initialisation (replaces Application_Start try/catch)
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, builder.Environment.ContentRootPath);
}
catch (Exception ex)
{
    // Do not take the whole application down: the pages already report a
    // failure to read documents, and this way the error is visible in one place.
    var bootstrapLogger = LoggerFactory.Create(lb => lb.AddConsole())
        .CreateLogger("Startup");
    bootstrapLogger.LogError(ex, "Could not prepare the database");
}

// ---------------------------------------------------------------------------
// TLS 1.2 — ServicePointManager.SecurityProtocol is removed.
// .NET Core / .NET 5+ defaults to the operating system's strongest TLS
// version, which is TLS 1.2+ on all supported platforms.
// ---------------------------------------------------------------------------

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see Microsoft docs).
// UseExceptionHandler → UseHsts → UseHttpsRedirection → UseStaticFiles →
// UseRouting → UseAntiforgery → endpoints.
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

// Application_Error → centralised exception logging.  In production the
// UseExceptionHandler above re-executes to ErrorPage; this middleware
// additionally logs the exception the way the legacy handler did.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled application error");
        throw; // re-throw so UseExceptionHandler can handle it
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Modern Blazor Server entry point (.NET 8+).
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
