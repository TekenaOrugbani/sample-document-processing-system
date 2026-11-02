using Microsoft.Extensions.Configuration;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options for the "Database" section of appsettings.json.
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        public bool UseSecretsManager { get; set; } = false;
        public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";
    }

    /// <summary>
    /// Strongly-typed options for the "Bedrock" section of appsettings.json.
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
    /// Strongly-typed options for the "Storage" section of appsettings.json.
    /// </summary>
    public class StorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>Virtual path; resolve with IWebHostEnvironment before use.</summary>
        public string RootPath { get; set; } = "~/App_Data/uploads";
        public int MaxFileSizeMegabytes { get; set; } = 50;
        public int MaxFilesPerUpload { get; set; } = 10;
        public string AllowedExtensions { get; set; } = ".pdf,.txt,.log";
    }


    /// <summary>
    /// Provides the same property surface as the legacy static AppSettings class,
    /// backed by IConfiguration / appsettings.json instead of ConfigurationManager / Web.config.
    /// Register as a singleton in DI so existing callers can inject it.
    /// </summary>
    public class AppSettings
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseOptions _database;
        private readonly BedrockOptions _bedrock;
        private readonly StorageOptions _storage;

        public AppSettings(IConfiguration configuration)
        {
            _configuration = configuration;

            _database = new DatabaseOptions();
            configuration.GetSection(DatabaseOptions.SectionName).Bind(_database);

            _bedrock = new BedrockOptions();
            configuration.GetSection(BedrockOptions.SectionName).Bind(_bedrock);

            _storage = new StorageOptions();
            configuration.GetSection(StorageOptions.SectionName).Bind(_storage);
        }

        public string ConnectionString
        {
            get
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (connectionString == null)
                {
                    throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from configuration.");
                }

                return connectionString;
            }
        }

        public bool DatabaseUseSecretsManager => _database.UseSecretsManager;

        public string DatabaseSecretDescriptionPrefix => _database.SecretDescriptionPrefix;

        public string BedrockRegion => _bedrock.Region;

        public string BedrockSummarizationModelId => _bedrock.SummarizationModelId;

        public int BedrockMaxTokens => _bedrock.MaxTokens;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int BedrockMaxInputCharacters => _bedrock.MaxInputCharacters;

        public int BedrockMaxPdfPages => _bedrock.MaxPdfPages;

        /// <summary>Virtual path; resolve with IWebHostEnvironment before use.</summary>
        public string StorageRootPath => _storage.RootPath;

        public int StorageMaxFileSizeMegabytes => _storage.MaxFileSizeMegabytes;

        public long StorageMaxFileSizeBytes => StorageMaxFileSizeMegabytes * 1024L * 1024L;

        public int StorageMaxFilesPerUpload => _storage.MaxFilesPerUpload;

        public string[] StorageAllowedExtensions =>
            _storage.AllowedExtensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }
}