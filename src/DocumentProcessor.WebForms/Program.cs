using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SecretsManager;
using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration – strongly-typed options bound from appsettings.json
// ---------------------------------------------------------------------------
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<BedrockOptions>(
    builder.Configuration.GetSection(BedrockOptions.SectionName));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

// AppSettings wraps IConfiguration and exposes the same property surface
// the legacy static class did. Register as singleton so components can
// @inject AppSettings.
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// Database – EF Core replaces EF6 / DocumentDbContextFactory
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "The 'DefaultConnection' connection string is missing from configuration.");

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connectionString));

// DatabaseConnectionResolver.Resolve() used to run at Application_Start to
// produce a DatabaseInfo and connection string (optionally from Secrets Manager).
// We resolve it once at startup and register the result as a singleton so
// MainLayout can @inject DatabaseInfo?.
builder.Services.AddSingleton<DatabaseInfo>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    var connection = DatabaseConnectionResolver.Resolve(appSettings);
    if (connection.Warning != null)
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Database connection resolver: {Warning}", connection.Warning);
    }
    return connection.Info;
});

// DocumentDbContextFactory is still used by DocumentPipeline for its
// unit-of-work pattern. Register as singleton with the resolved connection
// string so it can create short-lived contexts.
builder.Services.AddSingleton<DocumentDbContextFactory>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettings>();
    return new DocumentDbContextFactory(appSettings.ConnectionString);
});

// ---------------------------------------------------------------------------
// AWS SDK v4 – register via AddAWSService<>() for DI integration
// ---------------------------------------------------------------------------
builder.Services.AddAWSService<IAmazonBedrockRuntime>(new AWSOptions
{
    Region = RegionEndpoint.GetBySystemName(
        builder.Configuration.GetSection("Bedrock")["Region"] ?? "us-east-1")
});

builder.Services.AddAWSService<IAmazonSecretsManager>();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database initialisation (was Application_Start)
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.EnsureSchema(connectionString, app.Environment);
}
catch (Exception ex)
{
    // Do not take the whole application down: the pages already report a
    // failure to read documents, and this way the error is visible in one place.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline – order is load-bearing
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

// Root-route redirect — Default.aspx served "/" in Web Forms. Once
// Default.aspx is ported to a Blazor page at @page "/", remove this.
app.MapGet("/", context =>
{
    context.Response.Redirect("/Default");
    return Task.CompletedTask;
});

app.Run();
