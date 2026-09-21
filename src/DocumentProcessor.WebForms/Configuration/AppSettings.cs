using System;
using System.Configuration;

namespace DocumentProcessor.WebForms.Configuration
{
    /// <summary>
    /// Reads settings straight out of Web.config. No binding, no validation on startup --
    /// a bad value surfaces as a FormatException the first time the setting is touched.
    /// </summary>
    public static class AppSettings
    {
        public static string ConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings["DefaultConnection"];
                if (setting == null)
                {
                    throw new ConfigurationErrorsException(
                        "The 'DefaultConnection' connection string is missing from Web.config.");
                }

                return setting.ConnectionString;
            }
        }

        public static bool DatabaseUseSecretsManager
        {
            get { return GetBoolean("Database.UseSecretsManager", false); }
        }

        public static string DatabaseSecretDescriptionPrefix
        {
            get { return GetString("Database.SecretDescriptionPrefix", "Password for RDS MSSQL used for MAM319."); }
        }

        public static string BedrockRegion
        {
            get { return GetString("Bedrock.Region", "us-east-1"); }
        }

        public static string BedrockSummarizationModelId
        {
            get { return GetString("Bedrock.SummarizationModelId", "global.anthropic.claude-sonnet-5"); }
        }

        public static int BedrockMaxTokens
        {
            get { return GetInt32("Bedrock.MaxTokens", 2000); }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public static int BedrockMaxInputCharacters
        {
            get { return GetInt32("Bedrock.MaxInputCharacters", 10000); }
        }

        public static int BedrockMaxPdfPages
        {
            get { return GetInt32("Bedrock.MaxPdfPages", 5); }
        }

        /// <summary>Virtual path; resolve with HostingEnvironment.MapPath before use.</summary>
        public static string StorageRootPath
        {
            get { return GetString("Storage.RootPath", "~/App_Data/uploads"); }
        }

        public static int StorageMaxFileSizeMegabytes
        {
            get { return GetInt32("Storage.MaxFileSizeMegabytes", 50); }
        }

        public static long StorageMaxFileSizeBytes
        {
            get { return StorageMaxFileSizeMegabytes * 1024L * 1024L; }
        }

        public static int StorageMaxFilesPerUpload
        {
            get { return GetInt32("Storage.MaxFilesPerUpload", 10); }
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
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        private static int GetInt32(string key, int fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value.Trim());
        }

        private static bool GetBoolean(string key, bool fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : bool.Parse(value.Trim());
        }
    }
}
