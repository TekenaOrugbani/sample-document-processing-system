using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration — bind the strongly-typed options that the old static
// AppSettings class exposed. The Phase-2 migration already created
// DatabaseOptions / BedrockOptions / StorageOptions in Configuration/.
// ---------------------------------------------------------------------------
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(options =>
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);
builder.Services.Configure<BedrockOptions>(builder.Configuration.GetSection(BedrockOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

// ---------------------------------------------------------------------------
// Database — resolve the connection string (Web.config fallback or AWS
// Secrets Manager) and register EF Core with SQL Server.
// ---------------------------------------------------------------------------
var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                                 ?? string.Empty;
var useSecretsManager = builder.Configuration.GetValue<bool>("Database:UseSecretsManager");
var secretPrefix = builder.Configuration.GetValue<string>("Database:SecretDescriptionPrefix")
                   ?? "Password for RDS MSSQL used for MAM417.";

DatabaseConnection databaseConnection;
if (useSecretsManager)
{
    try
    {
        databaseConnection = await DatabaseConnectionResolver.ResolveFromSecretsManagerAsync(secretPrefix);
    }
    catch (Exception ex)
    {
        // Fall back to the configured connection string, exactly as Global.asax did.
        var fallbackInfo = new DatabaseInfo(
            DatabaseConnectionResolver.DetectProvider(configuredConnectionString),
            "appsettings.json",
            DatabaseConnectionResolver.HostOf(configuredConnectionString));
        databaseConnection = new DatabaseConnection(
            configuredConnectionString,
            fallbackInfo,
            "Secrets Manager lookup failed (" + ex.Message + "); using the configured connection string.");
    }
}
else
{
    var info = new DatabaseInfo(
        DatabaseConnectionResolver.DetectProvider(configuredConnectionString),
        "appsettings.json",
        DatabaseConnectionResolver.HostOf(configuredConnectionString));
    databaseConnection = new DatabaseConnection(configuredConnectionString, info);
}

if (databaseConnection.Warning is not null)
{
    // Log the warning once the host is built (logger is not available yet).
    // We capture it here and log it below after Build().
}

builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(databaseConnection.ConnectionString));

// ---------------------------------------------------------------------------
// DatabaseInfo — singleton so that the layout can @inject it to show the
// connection banner (replaces HttpApplication state Application[DatabaseInfoKey]).
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(databaseConnection.Info);

// ---------------------------------------------------------------------------
// Application services — replace the legacy "new" calls in Global.asax /
// Default.aspx code-behind with proper DI registrations.
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
// Startup logging — replaces Trace.Listeners / Trace.AutoFlush setup
// ---------------------------------------------------------------------------
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

if (databaseConnection.Warning is not null)
{
    startupLogger.LogWarning("{Warning}", databaseConnection.Warning);
}

// [PORT-NOTE] Dropped: ServicePointManager.SecurityProtocol TLS 1.2 pinning —
// .NET 10 negotiates TLS 1.2+ by default; explicit pinning is unnecessary.

// ---------------------------------------------------------------------------
// Database schema initialization — replaces DatabaseInitializer.EnsureSchema()
// called from Application_Start. Uses the Schema.sql script under App_Data/.
// ---------------------------------------------------------------------------
try
{
    var schemaPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "Schema.sql");
    if (File.Exists(schemaPath))
    {
        var script = await File.ReadAllTextAsync(schemaPath);
        var csBuilder = new SqlConnectionStringBuilder(databaseConnection.ConnectionString);
        var databaseName = csBuilder.InitialCatalog;

        // Create the database if it does not exist (reconnect to master).
        if (!string.IsNullOrEmpty(databaseName))
        {
            var masterBuilder = new SqlConnectionStringBuilder(csBuilder.ConnectionString)
            {
                InitialCatalog = "master"
            };
            const string createDbSql =
                "IF DB_ID(@name) IS NULL " +
                "BEGIN " +
                "  DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); " +
                "  EXEC sp_executesql @sql; " +
                "END";
            await using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
            await masterConn.OpenAsync();
            await using var createCmd = new SqlCommand(createDbSql, masterConn);
            createCmd.Parameters.AddWithValue("@name", databaseName);
            await createCmd.ExecuteNonQueryAsync();
        }

        // Run the schema script against the application database.
        await using var conn = new SqlConnection(databaseConnection.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(script, conn);
        await cmd.ExecuteNonQueryAsync();
    }
    else
    {
        startupLogger.LogWarning("Schema script not found at {Path}; skipping database initialization.", schemaPath);
    }
}
catch (Exception ex)
{
    // Do not take the whole application down, matching the original Global.asax behavior.
    startupLogger.LogError(ex, "Could not prepare the database.");
}

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see ASP.NET Core docs).
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// ---------------------------------------------------------------------------
// Blazor Server endpoint — modern .NET 8+ pattern (no MapBlazorHub / _Host).
// ---------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.Run();
