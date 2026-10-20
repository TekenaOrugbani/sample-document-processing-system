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
    /// Reads settings from appsettings.json via IConfiguration.
    /// Call <see cref="Initialize(IConfiguration)"/> from Program.cs at startup.
    /// </summary>
    public static class AppSettings
    {
        private static IConfiguration _configuration = null!;

        /// <summary>
        /// Must be called once at startup (e.g. in Program.cs) with the app's IConfiguration.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public static string ConnectionString
        {
            get
            {
                var setting = _configuration.GetConnectionString("DefaultConnection");
                if (setting == null)
                {
                    throw new InvalidOperationException("The 'DefaultConnection' connection string is missing from configuration.");
                }

                return setting;
            }
        }

        public static bool DatabaseUseSecretsManager
        {
            get
            {
                return GetBoolean("Database.UseSecretsManager", false);
            }
        }

        public static string DatabaseSecretDescriptionPrefix
        {
            get
            {
                return GetString("Database.SecretDescriptionPrefix", "Password for RDS MSSQL used for MAM417.");
            }
        }

        public static string BedrockRegion
        {
            get
            {
                return GetString("Bedrock.Region", "us-east-1");
            }
        }

        public static string BedrockSummarizationModelId
        {
            get
            {
                return GetString("Bedrock.SummarizationModelId", "global.anthropic.claude-sonnet-5");
            }
        }

        public static int BedrockMaxTokens
        {
            get
            {
                return GetInt32("Bedrock.MaxTokens", 2000);
            }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public static int BedrockMaxInputCharacters
        {
            get
            {
                return GetInt32("Bedrock.MaxInputCharacters", 10000);
            }
        }

        public static int BedrockMaxPdfPages
        {
            get
            {
                return GetInt32("Bedrock.MaxPdfPages", 5);
            }
        }

        /// <summary>Virtual path; resolve with HostingEnvironment.MapPath before use.</summary>
        public static string StorageRootPath
        {
            get
            {
                return GetString("Storage.RootPath", "~/App_Data/uploads");
            }
        }

        public static int StorageMaxFileSizeMegabytes
        {
            get
            {
                return GetInt32("Storage.MaxFileSizeMegabytes", 50);
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
                return GetInt32("Storage.MaxFilesPerUpload", 10);
            }
        }

        public static string[] StorageAllowedExtensions
        {
            get
            {
                var raw = GetString("Storage.AllowedExtensions", ".pdf,.txt,.log");
                return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static string GetString(string key, string fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        private static int GetInt32(string key, int fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value.Trim());
        }

        private static bool GetBoolean(string key, bool fallback)
        {
            var value = _configuration[key];
            return string.IsNullOrEmpty(value) ? fallback : bool.Parse(value.Trim());
        }
    }
}