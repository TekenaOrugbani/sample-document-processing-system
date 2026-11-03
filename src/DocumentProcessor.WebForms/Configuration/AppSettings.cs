using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Strongly-typed options for database-related configuration.
    /// Bind to the "Database" section in appsettings.json.
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        public bool UseSecretsManager { get; set; } = false;
        public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";
    }

    /// <summary>
    /// Strongly-typed options for Amazon Bedrock configuration.
    /// Bind to the "Bedrock" section in appsettings.json.
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
    /// Strongly-typed options for document storage configuration.
    /// Bind to the "Storage" section in appsettings.json.
    /// </summary>
    public class StorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>Virtual path; resolve with IWebHostEnvironment before use.</summary>
        public string RootPath { get; set; } = "~/App_Data/uploads";
        public int MaxFileSizeMegabytes { get; set; } = 50;
        public long MaxFileSizeBytes => MaxFileSizeMegabytes * 1024L * 1024L;
        public int MaxFilesPerUpload { get; set; } = 10;
        public string AllowedExtensions { get; set; } = ".pdf,.txt,.log";

        public string[] GetAllowedExtensionsArray()
        {
            return AllowedExtensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    /// <summary>
    /// Static facade over IConfiguration that preserves the original AppSettings surface
    /// so existing callers compile without changes. Call <see cref="Initialize"/> from
    /// Program.cs / startup to wire it to the ASP.NET Core configuration system.
    /// </summary>
    // PORT-TODO
    // {"note": "Callers of AppSettings should be migrated to inject IOptions<DatabaseOptions>, IOptions<BedrockOptions>, or IOptions<StorageOptions> directly via DI instead of using this static facade. Once all callers are migrated, this static class can be removed.", "files": ["Program.cs"]}
    public static class AppSettings
    {
        private static IConfiguration? _configuration;

        /// <summary>
        /// Initializes the static AppSettings with an IConfiguration instance.
        /// Call this early in Program.cs after building configuration.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private static IConfiguration Configuration =>
            _configuration ?? throw new InvalidOperationException(
                "AppSettings has not been initialized. Call AppSettings.Initialize(configuration) in Program.cs.");

        public static string ConnectionString
        {
            get
            {
                var connectionString = Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from configuration.");
                }

                return connectionString;
            }
        }

        public static bool DatabaseUseSecretsManager
        {
            get
            {
                return GetBoolean("Database:UseSecretsManager", false);
            }
        }

        public static string DatabaseSecretDescriptionPrefix
        {
            get
            {
                return GetString("Database:SecretDescriptionPrefix", "Password for RDS MSSQL used for MAM417.");
            }
        }

        public static string BedrockRegion
        {
            get
            {
                return GetString("Bedrock:Region", "us-east-1");
            }
        }

        public static string BedrockSummarizationModelId
        {
            get
            {
                return GetString("Bedrock:SummarizationModelId", "global.anthropic.claude-sonnet-5");
            }
        }

        public static int BedrockMaxTokens
        {
            get
            {
                return GetInt32("Bedrock:MaxTokens", 2000);
            }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public static int BedrockMaxInputCharacters
        {
            get
            {
                return GetInt32("Bedrock:MaxInputCharacters", 10000);
            }
        }

        public static int BedrockMaxPdfPages
        {
            get
            {
                return GetInt32("Bedrock:MaxPdfPages", 5);
            }
        }

        /// <summary>Virtual path; resolve with IWebHostEnvironment before use.</summary>
        public static string StorageRootPath
        {
            get
            {
                return GetString("Storage:RootPath", "~/App_Data/uploads");
            }
        }

        public static int StorageMaxFileSizeMegabytes
        {
            get
            {
                return GetInt32("Storage:MaxFileSizeMegabytes", 50);
            }
        }

        public static long StorageMaxFileSizeBytes
        {
            get
            {
                return StorageMaxFileSizeMegabytes * 1024L * 1024L;
            }
        }

        public static int StorageMaxFilesPerUpload
        {
            get
            {
                return GetInt32("Storage:MaxFilesPerUpload", 10);
            }
        }

        public static string[] StorageAllowedExtensions
        {
            get
            {
                var raw = GetString("Storage:AllowedExtensions", ".pdf,.txt,.log");
                return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static string GetString(string key, string fallback)
        {
            var value = Configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        private static int GetInt32(string key, int fallback)
        {
            var value = Configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value.Trim());
        }

        private static bool GetBoolean(string key, bool fallback)
        {
            var value = Configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : bool.Parse(value.Trim());
        }
    }
}