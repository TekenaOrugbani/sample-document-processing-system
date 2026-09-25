using DocumentProcessor.WebForms.Components;
using DocumentProcessor.WebForms.Configuration;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Kestrel – the legacy Web.config allowed ~500 MB uploads (maxRequestLength
// + requestLimits) with a 600 s execution timeout.  Carry that over.
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // ~500 MB
});

// ---------------------------------------------------------------------------
// Configuration – bind the strongly-typed AppSettings from appsettings.json.
// Components inject IOptions<AppSettings> / IOptionsSnapshot<AppSettings>.
// ---------------------------------------------------------------------------
var appSettingsSection = builder.Configuration.GetSection("AppSettings");
builder.Services.Configure<AppSettings>(appSettingsSection);

// Eagerly bind a snapshot for startup use (DatabaseConnectionResolver, etc.).
var appSettings = new AppSettings();
appSettingsSection.Bind(appSettings);

// The ConnectionString property is populated from the ConnectionStrings section.
appSettings.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? string.Empty;

// ---------------------------------------------------------------------------
// EF Core – DocumentDbContext was ported to EF Core in Phase 2.
// ---------------------------------------------------------------------------
builder.Services.AddDbContextFactory<DocumentDbContext>(options =>
    options.UseSqlServer(appSettings.ConnectionString));

// ---------------------------------------------------------------------------
// Database bootstrap – resolve connection & ensure schema at startup,
// replacing the Application_Start logic in Global.asax.cs.
// ---------------------------------------------------------------------------
var connection = await DatabaseConnectionResolver.ResolveAsync(appSettings);

if (connection.Warning is not null)
{
    var startupLogger = LoggerFactory
        .Create(lb => lb.AddConfiguration(builder.Configuration.GetSection("Logging")).AddConsole())
        .CreateLogger("Startup");
    startupLogger.LogWarning("{Warning}", connection.Warning);
}

// Register the resolved DatabaseInfo as a singleton so MainLayout can
// @inject it and display the provider/host badge.
builder.Services.AddSingleton(connection.Info);

try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, builder.Environment.ContentRootPath);
}
catch (Exception ex)
{
    // Mirror the original Global.asax behaviour: log and continue so the
    // application can at least start and report the error in-page.
    var startupLogger = LoggerFactory
        .Create(lb => lb.AddConfiguration(builder.Configuration.GetSection("Logging")).AddConsole())
        .CreateLogger("Startup");
    startupLogger.LogError(ex, "Could not prepare the database");
}

// [PORT-NOTE] Dropped ServicePointManager.SecurityProtocol TLS 1.2 pinning –
// .NET 10 negotiates TLS 1.2+ by default.

// [PORT-NOTE] Dropped Trace.Listeners.Add(TextWriterTraceListener) – ASP.NET
// Core logging (ILogger<T>) replaces System.Diagnostics.Trace throughout.

// ---------------------------------------------------------------------------
// Application services – registered in DI instead of being new'd inline.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DocumentTextExtractor>(sp =>
    new DocumentTextExtractor(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>()));

builder.Services.AddScoped<IDocumentStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value;

    // HostingEnvironment.MapPath("~/App_Data/uploads") → ContentRootPath + relative path.
    var storagePath = settings.StorageRootPath;
    if (!Path.IsPathRooted(storagePath))
    {
        storagePath = Path.Combine(env.ContentRootPath, storagePath);
    }

    return new FileSystemDocumentStorage(storagePath);
});

builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentPipeline>();

// ---------------------------------------------------------------------------
// Blazor Server
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline — order is load-bearing (see Microsoft docs).
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

// Modern Blazor Server entry point (.NET 8+).
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
