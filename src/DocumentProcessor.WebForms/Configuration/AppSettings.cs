using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options POCO bound from the "AppSettings" section of appsettings.json.
    /// Register in DI with: services.Configure&lt;AppSettingsOptions&gt;(config.GetSection("AppSettings"));
    /// </summary>
    public class AppSettingsOptions
    {
        public DatabaseOptions Database { get; set; } = new();
        public BedrockOptions Bedrock { get; set; } = new();
        public StorageOptions Storage { get; set; } = new();

        public class DatabaseOptions
        {
            public bool UseSecretsManager { get; set; } = false;
            public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM319.";
        }

        public class BedrockOptions
        {
            public string Region { get; set; } = "us-east-1";
            public string SummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";
            public int MaxTokens { get; set; } = 2000;
            public int MaxInputCharacters { get; set; } = 10000;
            public int MaxPdfPages { get; set; } = 5;
        }

        public class StorageOptions
        {
            public string RootPath { get; set; } = "~/App_Data/uploads";
            public int MaxFileSizeMegabytes { get; set; } = 50;
            public int MaxFilesPerUpload { get; set; } = 10;
            public string AllowedExtensions { get; set; } = ".pdf,.txt,.log";
        }
    }

    /// <summary>
    /// Reads settings from IConfiguration / IOptions&lt;AppSettingsOptions&gt; (appsettings.json).
    /// Preserves the same public property surface as the former static class so callers
    /// can be migrated incrementally to constructor-inject this service.
    /// Register as a singleton in DI: services.AddSingleton&lt;AppSettings&gt;();
    /// </summary>
    public class AppSettings
    {
        private readonly IConfiguration _configuration;
        private readonly AppSettingsOptions _options;

        public AppSettings(IConfiguration configuration, IOptions<AppSettingsOptions> options)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public string ConnectionString
        {
            get
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from appsettings.json.");
                }

                return connectionString;
            }
        }

        public bool DatabaseUseSecretsManager
        {
            get
            {
                return _options.Database.UseSecretsManager;
            }
        }

        public string DatabaseSecretDescriptionPrefix
        {
            get
            {
                return _options.Database.SecretDescriptionPrefix;
            }
        }

        public string BedrockRegion
        {
            get
            {
                return _options.Bedrock.Region;
            }
        }

        public string BedrockSummarizationModelId
        {
            get
            {
                return _options.Bedrock.SummarizationModelId;
            }
        }

        public int BedrockMaxTokens
        {
            get
            {
                return _options.Bedrock.MaxTokens;
            }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters
        {
            get
            {
                return _options.Bedrock.MaxInputCharacters;
            }
        }

        public int BedrockMaxPdfPages
        {
            get
            {
                return _options.Bedrock.MaxPdfPages;
            }
        }

        /// <summary>Virtual path; resolve with IWebHostEnvironment.ContentRootPath before use.</summary>
        public string StorageRootPath
        {
            get
            {
                return _options.Storage.RootPath;
            }
        }

        public int StorageMaxFileSizeMegabytes
        {
            get
            {
                return _options.Storage.MaxFileSizeMegabytes;
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
                return _options.Storage.MaxFilesPerUpload;
            }
        }

        public string[] StorageAllowedExtensions
        {
            get
            {
                var raw = _options.Storage.AllowedExtensions;
                return (raw ?? ".pdf,.txt,.log").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }
}