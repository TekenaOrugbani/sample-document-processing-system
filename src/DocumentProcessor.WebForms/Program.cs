using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// AppSettings — wraps IConfiguration; registered as a singleton so all
// consumers share one instance (replaces the legacy static AppSettings class).
builder.Services.AddSingleton<AppSettings>();

// DatabaseConnectionResolver.Resolve resolves the connection from either
// appsettings.json or AWS Secrets Manager, exactly as Global.asax
// Application_Start did. Register the resolved DatabaseConnection and
// DatabaseInfo as singletons for injection throughout the app.
// PORT-TODO
// {"was": "DocumentProcessor.WebForms.Data.DatabaseConnectionResolver", "note": "DatabaseConnectionResolver.Resolve() is static and still calls AppSettings properties as if static. Since AppSettings is now instance-based (takes IConfiguration via constructor), DatabaseConnectionResolver must be updated to accept an AppSettings parameter: public static DatabaseConnection Resolve(AppSettings settings). Until then, Program.cs resolves AppSettings early and passes it through.", "files": ["Data/DatabaseConnectionResolver.cs"]}
builder.Services.AddSingleton<DatabaseConnection>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    return DatabaseConnectionResolver.ResolveAsync(settings).GetAwaiter().GetResult();
});

builder.Services.AddSingleton<DatabaseInfo>(sp =>
{
    var connection = sp.GetRequiredService<DatabaseConnection>();
    return connection.Info;
});

// EF Core DbContext — registered with the connection resolved at runtime
// from DatabaseConnectionResolver (Secrets Manager or appsettings.json).
builder.Services.AddDbContext<DocumentDbContext>((sp, options) =>
{
    var conn = sp.GetRequiredService<DatabaseConnection>();
    options.UseSqlServer(conn.ConnectionString);
});

// Document services — currently instantiated directly (no DI in the legacy code),
// registered here so ported Blazor components can receive them via @inject.
builder.Services.AddSingleton<DocumentTextExtractor>();

// PORT-TODO
// {"was": "System.Web.Hosting.HostingEnvironment.MapPath", "note": "FileSystemDocumentStorage constructor currently calls HostingEnvironment.MapPath(AppSettings.StorageRootPath) which is not available on .NET Core. The constructor must be updated to accept a string rootPath parameter. Program.cs passes the resolved physical path via the factory lambda below.", "files": ["Services/FileSystemDocumentStorage.cs"]}
builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var settings = sp.GetRequiredService<AppSettings>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storagePath = settings.StorageRootPath;

    // The legacy app used HostingEnvironment.MapPath("~/App_Data/uploads").
    // Resolve the virtual path to a physical path under ContentRootPath.
    string rootPath;
    if (storagePath.StartsWith("~/"))
    {
        rootPath = Path.Combine(env.ContentRootPath, storagePath.Substring(2).Replace('/', Path.DirectorySeparatorChar));
    }
    else
    {
        rootPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(env.ContentRootPath, storagePath);
    }

    return new FileSystemDocumentStorage(rootPath);
});

// PORT-TODO
// {"was": "DocumentProcessor.WebForms.Services.BedrockDocumentSummarizer", "note": "BedrockDocumentSummarizer reads AppSettings statically and calls Client.Converse() synchronously. The .NET 10.0 AWS SDK only provides ConverseAsync(), so the Summarize method and entire call chain (DocumentPipeline.Process -> IDocumentSummarizer.Summarize) must become async. Constructor must accept AppSettings via DI.", "files": ["Services/BedrockDocumentSummarizer.cs", "Services/IDocumentSummarizer.cs", "Services/DocumentPipeline.cs"]}
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();

builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup bootstrap (carried over from Application_Start)
// ---------------------------------------------------------------------------

// 1. TLS pinning — .NET 10.0 defaults to TLS 1.2+ and ServicePointManager is
//    not available / not relevant on modern .NET. No action needed.
//    [PORT-NOTE] Dropped ServicePointManager.SecurityProtocol TLS 1.2 pinning:
//    .NET 10.0 defaults to TLS 1.2+ and the API is not available.

// 2. Resolve the database connection (Secrets Manager or appsettings.json) and
//    set the static ConnectionString that EF6 DocumentDbContext still reads.
var connection = app.Services.GetRequiredService<DatabaseConnection>();

if (connection.Warning != null)
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogWarning("{Warning}", connection.Warning);
}

// 3. Ensure the database schema exists (same as the legacy DatabaseInitializer).
// PORT-TODO
// {"was": "System.Web.Hosting.HostingEnvironment.MapPath", "note": "DatabaseInitializer.RunSchemaScript uses HostingEnvironment.MapPath to locate App_Data/Schema.sql. Must be updated to accept a contentRootPath parameter or IWebHostEnvironment. Program.cs passes app.Environment.ContentRootPath.", "files": ["Data/DatabaseInitializer.cs"]}
try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogError(ex, "Could not prepare the database.");
}

// [PORT-NOTE] Dropped TextWriterTraceListener to ~/App_Data/trace.log: ASP.NET
// Core uses ILogger infrastructure (configured in appsettings.json Logging section).

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

// Application_Error → centralised exception handler for unhandled exceptions.
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
        throw; // Re-throw so UseExceptionHandler can handle the response.
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Status code pages — the legacy app had customErrors with a 404 redirect.
app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Root route coverage: the legacy Default.aspx served at "/". When the ported
// Default page owns @page "/", this redirect is unnecessary. If not yet ported,
// redirect to an existing page so "/" does not 404.
app.MapGet("/default.aspx", context =>
{
    context.Response.Redirect("/");
    return Task.CompletedTask;
});

app.Run();
