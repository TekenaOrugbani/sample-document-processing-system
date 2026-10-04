using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Options & configuration
// ---------------------------------------------------------------------------

// Bind the strongly-typed options POCO from appsettings.json sections.
builder.Services.Configure<AppSettingsOptions>(options =>
{
    builder.Configuration.GetSection("Database").Bind(options.Database);
    builder.Configuration.GetSection("Bedrock").Bind(options.Bedrock);
    builder.Configuration.GetSection("Storage").Bind(options.Storage);
});

// AppSettings is the instance-based replacement for the former static helper.
// Services that still reference it (DatabaseConnectionResolver, FileSystemDocumentStorage,
// DocumentTextExtractor, BedrockDocumentSummarizer) can constructor-inject it once ported.
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// Database bootstrap (was Application_Start in Global.asax)
// ---------------------------------------------------------------------------

// Resolve the connection at startup and register the result as a singleton.
// DatabaseConnectionResolver.Resolve() needs an AppSettings instance, so we
// build it eagerly from the already-available IConfiguration + IOptions.
builder.Services.AddSingleton(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    return DatabaseConnectionResolver.Resolve(appSettings);
});

// DatabaseInfo is injected by MainLayout.razor (replaces Application["DatabaseInfoKey"]).
builder.Services.AddSingleton(sp => sp.GetRequiredService<DatabaseConnection>().Info);

// PORT-TODO
// {"was": "System.Data.Entity.DbContext", "note": "DocumentDbContext inherits EF6 DbContext which does not support AddDbContext<T>. Once migrated to EF Core, replace this factory registration with builder.Services.AddDbContext<DocumentDbContext>(). For now, register a factory that supplies the resolved connection string."}

// Register DocumentDbContext as a transient factory so it receives the resolved
// connection string. EF6 does not support the AddDbContext pattern.
builder.Services.AddTransient(sp =>
{
    var connection = sp.GetRequiredService<DatabaseConnection>();
    DocumentDbContext.ConnectionString = connection.ConnectionString;
    return new DocumentDbContext();
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------

builder.Services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddSingleton<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddTransient<DocumentTextExtractor>();
builder.Services.AddTransient<DocumentPipeline>();
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation (was Application_Start in Global.asax)
// ---------------------------------------------------------------------------

// Run the schema script at startup, just as Global.asax did. Failures are logged
// but do not prevent the application from starting (matching original behaviour).
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var connection = app.Services.GetRequiredService<DatabaseConnection>();

    if (connection.Warning != null)
    {
        logger.LogWarning("Database connection warning: {Warning}", connection.Warning);
    }

    // Set the static connection string so any remaining code that instantiates
    // DocumentDbContext directly (e.g. DocumentPipeline, Default page) keeps working.
    DocumentDbContext.ConnectionString = connection.ConnectionString;

    try
    {
        DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment);
    }
    catch (Exception ex)
    {
        // Match original Global.asax behaviour: log but do not crash the app.
        logger.LogError(ex, "Could not prepare the database.");
    }
}

// [PORT-NOTE] TLS 1.2 pinning (ServicePointManager.SecurityProtocol) removed —
// .NET 10 negotiates TLS 1.2+ by default and ServicePointManager is not applicable.

// ---------------------------------------------------------------------------
// Middleware pipeline (order is load-bearing — see ASP.NET Core middleware order docs)
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

// Centralised exception logging (replaces Application_Error from Global.asax).
// UseExceptionHandler above handles the redirect; this middleware ensures every
// unhandled exception is logged even in Development.
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
        throw; // Re-throw so the exception handler middleware can render the error page.
    }
});

// Modern Blazor Server entry point (.NET 8+ pattern). NOT MapBlazorHub +
// MapFallbackToPage("/_Host").
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
