using System;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options class bound to appsettings.json.
    /// Register in DI with:
    ///   builder.Services.Configure&lt;AppSettings&gt;(builder.Configuration.GetSection("AppSettings"));
    /// and set ConnectionString separately:
    ///   builder.Services.PostConfigure&lt;AppSettings&gt;(opts =>
    ///       opts.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ///           ?? throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from appsettings.json."));
    /// Then inject IOptions&lt;AppSettings&gt; where needed.
    /// </summary>
    public class AppSettings
    {

        public string ConnectionString { get; set; } = string.Empty;

        public bool DatabaseUseSecretsManager { get; set; } = false;

        public string DatabaseSecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";

        public string BedrockRegion { get; set; } = "us-east-1";

        public string BedrockSummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";

        public int BedrockMaxTokens { get; set; } = 2000;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters { get; set; } = 10000;

        public int BedrockMaxPdfPages { get; set; } = 5;

        /// <summary>Virtual path; resolve with IWebHostEnvironment before use.</summary>
        public string StorageRootPath { get; set; } = "~/App_Data/uploads";

        public int StorageMaxFileSizeMegabytes { get; set; } = 50;

        public long StorageMaxFileSizeBytes => StorageMaxFileSizeMegabytes * 1024L * 1024L;

        public int StorageMaxFilesPerUpload { get; set; } = 10;

        public string StorageAllowedExtensionsRaw { get; set; } = ".pdf,.txt,.log";

        public string[] StorageAllowedExtensions =>
            StorageAllowedExtensionsRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }
}