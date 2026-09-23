using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Logging — replaces the TextWriterTraceListener from Application_Start.
// ASP.NET Core's built-in logging (console + debug) replaces Trace output.
// A file sink can be added via Serilog or a similar provider if needed.
// ---------------------------------------------------------------------------
builder.Logging.AddConsole();

// ---------------------------------------------------------------------------
// Configuration-based settings (replaces static AppSettings)
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AppSettings>();

// ---------------------------------------------------------------------------
// Database connection resolution — mirrors Application_Start logic.
// The original Global.asax.cs called DatabaseConnectionResolver.Resolve()
// which used static AppSettings members. AppSettings is now instance-based
// with IConfiguration DI, so we resolve the connection using IConfiguration
// directly at startup.
// ---------------------------------------------------------------------------
var appSettings = new AppSettings(builder.Configuration);
var connection = ResolveConnection(appSettings);

if (connection.Warning is not null)
{
    // Host is not yet built, so use Console for startup warnings.
    Console.WriteLine("[WARN] " + connection.Warning);
}

// Register the resolved DatabaseInfo as a singleton — replaces the
// Application[DatabaseInfoKey] global state that Site.Master read.
builder.Services.AddSingleton(connection.Info);

// ---------------------------------------------------------------------------
// Entity Framework Core — DocumentDbContext with SQL Server provider.
// The global query filter for soft-delete (!d.IsDeleted) is already
// configured in DocumentDbContext.OnModelCreating.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// ---------------------------------------------------------------------------
// Application services — all registered in DI as instructed
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
// Database initialization — replaces DatabaseInitializer.EnsureSchema call
// from Application_Start. Schema.sql is located via IWebHostEnvironment
// ContentRootPath instead of HostingEnvironment.MapPath.
// ---------------------------------------------------------------------------
try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    // Do not take the whole application down: the pages already report a
    // failure to read documents, and this way the error is visible in one place.
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see ASP.NET Core docs)
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
// Blazor Server endpoint — modern .NET 8+ MapRazorComponents pattern
// (NOT MapBlazorHub + MapFallbackToPage("/_Host"))
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// ---------------------------------------------------------------------------
// Local helper — replicates the DatabaseConnectionResolver.Resolve() startup
// chain using the instance-based AppSettings. This replaces the old static
// call that Application_Start made.
// ---------------------------------------------------------------------------
static DatabaseConnection ResolveConnection(AppSettings settings)
{
    var configured = settings.ConnectionString;
    var fromConfig = new DatabaseConnection(
        configured,
        new DatabaseInfo(
            DatabaseConnectionResolver.DetectProvider(configured),
            "appsettings.json",
            HostOf(configured)));

    if (!settings.DatabaseUseSecretsManager)
    {
        return fromConfig;
    }

    try
    {
        return FromSecretsManager(settings.DatabaseSecretDescriptionPrefix);
    }
    catch (Exception ex)
    {
        return new DatabaseConnection(
            fromConfig.ConnectionString,
            fromConfig.Info,
            "Secrets Manager lookup failed (" + ex.Message + "); using the configured connection string.");
    }
}

static DatabaseConnection FromSecretsManager(string descriptionPrefix)
{
    using var client = new Amazon.SecretsManager.AmazonSecretsManagerClient();

    var secretsResponse = client.ListSecretsAsync(
        new Amazon.SecretsManager.Model.ListSecretsRequest()).GetAwaiter().GetResult();

    var match = secretsResponse.SecretList?
        .FirstOrDefault(s => s.Description is not null
            && s.Description.StartsWith(descriptionPrefix, StringComparison.OrdinalIgnoreCase));

    if (match is null)
    {
        throw new InvalidOperationException(
            "No secret whose description starts with '" + descriptionPrefix + "'.");
    }

    var valueResponse = client.GetSecretValueAsync(
        new Amazon.SecretsManager.Model.GetSecretValueRequest { SecretId = match.ARN })
        .GetAwaiter().GetResult();

    var secret = Newtonsoft.Json.Linq.JObject.Parse(valueResponse.SecretString);
    var connBuilder = new SqlConnectionStringBuilder
    {
        DataSource = Field(secret, "host") + "," + Field(secret, "port"),
        InitialCatalog = Field(secret, "dbname"),
        UserID = Field(secret, "username"),
        Password = Field(secret, "password"),
        TrustServerCertificate = true,
        Encrypt = true
    };

    return new DatabaseConnection(
        connBuilder.ConnectionString,
        new DatabaseInfo(DatabaseProvider.SqlServer, "AWS Secrets Manager", connBuilder.DataSource));
}

static string HostOf(string connectionString)
{
    if (DatabaseConnectionResolver.DetectProvider(connectionString) == DatabaseProvider.SqlServer)
    {
        return new SqlConnectionStringBuilder(connectionString).DataSource;
    }

    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var part in parts)
    {
        var trimmed = part.Trim();
        if (trimmed.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed["Host=".Length..];
        }
    }

    return "unknown";
}

static string Field(Newtonsoft.Json.Linq.JObject secret, string name)
{
    var token = secret[name];
    if (token is null)
    {
        throw new InvalidOperationException("Secret is missing the '" + name + "' field.");
    }

    return token.ToString();
}
