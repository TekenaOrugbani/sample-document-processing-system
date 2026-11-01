using System;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options for the "Database" section of appsettings.json.
    /// </summary>
    public class DatabaseSettings
    {
        public const string SectionName = "Database";

        public bool UseSecretsManager { get; set; } = false;
        public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";
    }

    /// <summary>
    /// Strongly-typed options for the "Bedrock" section of appsettings.json.
    /// </summary>
    public class BedrockSettings
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
    /// Strongly-typed options for the "Storage" section of appsettings.json.
    /// </summary>
    public class StorageSettings
    {
        public const string SectionName = "Storage";

        /// <summary>Virtual path; resolve with IWebHostEnvironment.ContentRootPath before use.</summary>
        public string RootPath { get; set; } = "~/App_Data/uploads";
        public int MaxFileSizeMegabytes { get; set; } = 50;
        public int MaxFilesPerUpload { get; set; } = 10;
        public string AllowedExtensions { get; set; } = ".pdf,.txt,.log";
    }

    /// <summary>
    /// Reads settings from IConfiguration (appsettings.json / environment / etc.).
    /// Replaces the legacy static ConfigurationManager-based AppSettings class.
    /// Register as a singleton in DI and inject where needed, or use IOptions&lt;T&gt;
    /// for individual sections.
    /// </summary>
    public class AppSettings
    {
        private readonly IConfiguration _configuration;

        public AppSettings(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public string ConnectionString
        {
            get
            {
                var value = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from configuration.");
                }

                return value;
            }
        }

        public bool DatabaseUseSecretsManager
        {
            get
            {
                return GetBoolean("Database:UseSecretsManager", false);
            }
        }

        public string DatabaseSecretDescriptionPrefix
        {
            get
            {
                return GetString("Database:SecretDescriptionPrefix", "Password for RDS MSSQL used for MAM417.");
            }
        }

        public string BedrockRegion
        {
            get
            {
                return GetString("Bedrock:Region", "us-east-1");
            }
        }

        public string BedrockSummarizationModelId
        {
            get
            {
                return GetString("Bedrock:SummarizationModelId", "global.anthropic.claude-sonnet-5");
            }
        }

        public int BedrockMaxTokens
        {
            get
            {
                return GetInt32("Bedrock:MaxTokens", 2000);
            }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters
        {
            get
            {
                return GetInt32("Bedrock:MaxInputCharacters", 10000);
            }
        }

        public int BedrockMaxPdfPages
        {
            get
            {
                return GetInt32("Bedrock:MaxPdfPages", 5);
            }
        }

        /// <summary>Virtual path; resolve with IWebHostEnvironment.ContentRootPath before use.</summary>
        public string StorageRootPath
        {
            get
            {
                return GetString("Storage:RootPath", "~/App_Data/uploads");
            }
        }

        public int StorageMaxFileSizeMegabytes
        {
            get
            {
                return GetInt32("Storage:MaxFileSizeMegabytes", 50);
            }
        }

        public long StorageMaxFileSizeBytes
        {
            get
            {
                return StorageMaxFileSizeMegabytes * 1024L * 1024L;
            }
        }

        public int StorageMaxFilesPerUpload
        {
            get
            {
                return GetInt32("Storage:MaxFilesPerUpload", 10);
            }
        }

        public string[] StorageAllowedExtensions
        {
            get
            {
                var raw = GetString("Storage:AllowedExtensions", ".pdf,.txt,.log");
                return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private string GetString(string key, string fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        private int GetInt32(string key, int fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value.Trim());
        }

        private bool GetBoolean(string key, bool fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : bool.Parse(value.Trim());
        }
    }
}