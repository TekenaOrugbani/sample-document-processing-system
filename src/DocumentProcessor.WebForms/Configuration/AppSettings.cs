using System;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Options POCO bound from the "AppSettings" section of appsettings.json
    /// via <c>services.Configure&lt;AppSettings&gt;(configuration.GetSection("AppSettings"))</c>.
    /// Inject as <c>IOptions&lt;AppSettings&gt;</c> or <c>IOptionsSnapshot&lt;AppSettings&gt;</c>.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// The "DefaultConnection" connection string.
        /// Bind from <c>ConnectionStrings:DefaultConnection</c> in appsettings.json
        /// (typically resolved separately via <c>configuration.GetConnectionString("DefaultConnection")</c>
        /// rather than through this options class).
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

        /// <summary>Relative path under ContentRootPath for uploaded files.</summary>
        public string StorageRootPath { get; set; } = "App_Data/uploads";

        public int StorageMaxFileSizeMegabytes { get; set; } = 50;

        /// <summary>Computed from <see cref="StorageMaxFileSizeMegabytes"/>; not bound from config.</summary>
        public long StorageMaxFileSizeBytes => StorageMaxFileSizeMegabytes * 1024L * 1024L;

        public int StorageMaxFilesPerUpload { get; set; } = 10;

        /// <summary>
        /// Comma-separated in appsettings.json (e.g. ".pdf,.txt,.log").
        /// The setter splits the raw value; the getter returns the array.
        /// </summary>
        public string[] StorageAllowedExtensions { get; set; } = new[] { ".pdf", ".txt", ".log" };
    }
}