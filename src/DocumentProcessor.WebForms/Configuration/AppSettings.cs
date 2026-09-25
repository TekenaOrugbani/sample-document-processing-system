namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed settings class bound from the "AppSettings" section in appsettings.json.
    /// Register in DI with:
    ///   builder.Services.Configure&lt;AppSettings&gt;(builder.Configuration.GetSection("AppSettings"));
    /// Then inject IOptions&lt;AppSettings&gt; (or IOptionsSnapshot&lt;AppSettings&gt;) where needed.
    /// The ConnectionString property is populated separately from the "ConnectionStrings:DefaultConnection"
    /// configuration key — set it up in Program.cs or bind it in the same Configure call.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// The "DefaultConnection" connection string.
        /// Bind from Configuration.GetConnectionString("DefaultConnection") in Program.cs
        /// and assign to this property, or map it in the "AppSettings" config section.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        public bool DatabaseUseSecretsManager { get; set; } = false;

        public string DatabaseSecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM319.";

        public string BedrockRegion { get; set; } = "us-east-1";

        public string BedrockSummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";

        public int BedrockMaxTokens { get; set; } = 2000;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters { get; set; } = 10000;

        public int BedrockMaxPdfPages { get; set; } = 5;

        /// <summary>Physical or relative path to the upload storage root.</summary>
        public string StorageRootPath { get; set; } = "App_Data/uploads";

        public int StorageMaxFileSizeMegabytes { get; set; } = 50;

        public long StorageMaxFileSizeBytes => StorageMaxFileSizeMegabytes * 1024L * 1024L;

        public int StorageMaxFilesPerUpload { get; set; } = 10;

        public string StorageAllowedExtensionsRaw { get; set; } = ".pdf,.txt,.log";

        public string[] StorageAllowedExtensions =>
            StorageAllowedExtensionsRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }
}