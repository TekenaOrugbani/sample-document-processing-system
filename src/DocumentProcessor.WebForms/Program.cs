using Amazon;
using Amazon.BedrockRuntime;
using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Kestrel request-size limits — migrated from httpRuntime maxRequestLength
// (512 MB) and executionTimeout (600 s).
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    var maxBodySize = builder.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize")
                      ?? 524_288_000L;
    options.Limits.MaxRequestBodySize = maxBodySize;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize")
                                      ?? 524_288_000L;
});

// ---------------------------------------------------------------------------
// Blazor Server components
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// Configuration helper — reads IConfiguration (appsettings.json / env vars).
// Formerly a static class; now injected via DI.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// Database — EF Core DbContext with SQL Server provider.
// Replaces the static DocumentDbContext.ConnectionString set by Global.asax
// and the EF6 entityFramework configSection in Web.config.
// ---------------------------------------------------------------------------
builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException(
                               "The 'DefaultConnection' connection string is missing from configuration.");
    options.UseSqlServer(connectionString);
});

// ---------------------------------------------------------------------------
// DatabaseInfo — resolved once at startup so the layout banner can display
// the active provider, host, and credential source. Replaces the
// Application[DatabaseInfoKey] slot from Global.asax.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DatabaseConnectionResolver>();
// PORT-TODO
// {"note": "DatabaseConnectionResolver.Resolve() still uses static AppSettings access. Once DatabaseConnectionResolver is ported to accept AppSettings via parameter or DI, update this registration to pass the instance.", "was": "DocumentProcessor.WebForms.Data.DatabaseConnectionResolver.Resolve", "files": ["Data/DatabaseConnectionResolver.cs"]}
builder.Services.AddSingleton<DatabaseInfo>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var resolver = sp.GetRequiredService<DatabaseConnectionResolver>();
        var connection = resolver.Resolve();

        if (connection.Warning is not null)
        {
            logger.LogWarning("Database connection resolution warning: {Warning}", connection.Warning);
        }

        return connection.Info;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to resolve database connection at startup.");
        return new DatabaseInfo(DatabaseProvider.SqlServer, "unavailable", "unknown");
    }
});

// ---------------------------------------------------------------------------
// AWS Bedrock runtime client — singleton, thread-safe, expensive to build.
// Replaces the static field in BedrockDocumentSummarizer.
// ---------------------------------------------------------------------------
// PORT-TODO
// {"note": "BedrockDocumentSummarizer creates its own static IAmazonBedrockRuntime client using static AppSettings. Once that class is ported to accept IAmazonBedrockRuntime via constructor DI, this registration will supply the client.", "was": "DocumentProcessor.WebForms.Services.BedrockDocumentSummarizer.Client", "files": ["Services/BedrockDocumentSummarizer.cs"]}
builder.Services.AddSingleton<IAmazonBedrockRuntime>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    var config = new AmazonBedrockRuntimeConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(appSettings.BedrockRegion)
    };
    return new AmazonBedrockRuntimeClient(config);
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentTextExtractor>();
// PORT-TODO
// {"note": "DocumentPipeline uses parameterless constructor that creates its own dependencies via new. It must be updated to accept IDocumentStorage, DocumentTextExtractor, IDocumentSummarizer, and DocumentDbContext via constructor DI.", "was": "DocumentProcessor.WebForms.Services.DocumentPipeline", "files": ["Services/DocumentPipeline.cs"]}
builder.Services.AddScoped<DocumentPipeline>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database schema initialisation — migrated from Application_Start.
// Runs once at startup. Failures are logged, not fatal (matches original
// behaviour: "Do not take the whole application down").
// ---------------------------------------------------------------------------
try
{
    var resolver = app.Services.GetRequiredService<DatabaseConnectionResolver>();
    var connection = resolver.Resolve();
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing.
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    // Application_Error → centralised exception handler.
    // Redirects to the ported ErrorPage component at /ErrorPage.
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// ---------------------------------------------------------------------------
// Root-route coverage: Default.aspx was the default document under IIS;
// redirect "/" to the ported Default page. When a component claims
// @page "/" this redirect becomes a no-op and can be removed.
// ---------------------------------------------------------------------------
app.MapGet("/", context =>
{
    context.Response.Redirect("/Default");
    return Task.CompletedTask;
});

// ---------------------------------------------------------------------------
// Modern Blazor Server entry point — NOT MapBlazorHub / MapFallbackToPage.
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
