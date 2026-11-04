using System;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options bound from the "Database" section of appsettings.json.
    /// Register with: services.Configure&lt;DatabaseOptions&gt;(config.GetSection("Database"));
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>
        /// The DefaultConnection connection string.
        /// Bind from ConnectionStrings:DefaultConnection via
        /// services.Configure&lt;DatabaseOptions&gt;(o =&gt; o.ConnectionString = config.GetConnectionString("DefaultConnection"));
        /// or set it directly in the "Database" section.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        public bool UseSecretsManager { get; set; } = false;

        public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";
    }

    /// <summary>
    /// Strongly-typed options bound from the "Bedrock" section of appsettings.json.
    /// Register with: services.Configure&lt;BedrockOptions&gt;(config.GetSection("Bedrock"));
    /// </summary>
    public class BedrockOptions
    {
        public const string SectionName = "Bedrock";

        public string Region { get; set; } = "us-east-1";

        public string SummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";

        public int MaxTokens { get; set; } = 2000;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int MaxInputCharacters { get; set; } = 10000;

        public int MaxPdfPages { get; set; } = 5;
    }

    /// <summary>
    /// Strongly-typed options bound from the "Storage" section of appsettings.json.
    /// Register with: services.Configure&lt;StorageOptions&gt;(config.GetSection("Storage"));
    /// </summary>
    public class StorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>Relative path under ContentRootPath; resolve with IWebHostEnvironment before use.</summary>
        public string RootPath { get; set; } = "App_Data/uploads";

        public int MaxFileSizeMegabytes { get; set; } = 50;

        public long MaxFileSizeBytes => MaxFileSizeMegabytes * 1024L * 1024L;

        public int MaxFilesPerUpload { get; set; } = 10;

        public string AllowedExtensions { get; set; } = ".pdf,.txt,.log";

        /// <summary>Returns the allowed extensions as an array, splitting on commas.</summary>
        public string[] GetAllowedExtensionsArray()
        {
            return (AllowedExtensions ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

}