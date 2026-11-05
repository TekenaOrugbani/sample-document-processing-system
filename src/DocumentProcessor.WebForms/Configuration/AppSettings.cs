using System;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options class bound from the "AppSettings" section of appsettings.json.
    /// Register in Program.cs with:
    ///   builder.Services.Configure&lt;AppSettings&gt;(builder.Configuration.GetSection("AppSettings"));
    /// The connection string is read separately via IConfiguration.GetConnectionString("DefaultConnection").
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// The DefaultConnection connection string. Populated from ConnectionStrings:DefaultConnection
        /// in appsettings.json. Must be set explicitly during service configuration
        /// (e.g. options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")).
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        public bool DatabaseUseSecretsManager { get; set; } = false;

        public string DatabaseSecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";

        public string BedrockRegion { get; set; } = "us-east-1";

        public string BedrockSummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";

        public int BedrockMaxTokens { get; set; } = 2000;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters { get; set; } = 10000;

        public int BedrockMaxPdfPages { get; set; } = 5;

        /// <summary>Virtual path; resolve with IWebHostEnvironment.ContentRootPath before use.</summary>
        public string StorageRootPath { get; set; } = "~/App_Data/uploads";

        public int StorageMaxFileSizeMegabytes { get; set; } = 50;

        public long StorageMaxFileSizeBytes => StorageMaxFileSizeMegabytes * 1024L * 1024L;

        public int StorageMaxFilesPerUpload { get; set; } = 10;

        public string StorageAllowedExtensionsRaw { get; set; } = ".pdf,.txt,.log";

        public string[] StorageAllowedExtensions =>
            StorageAllowedExtensionsRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }
}