using System;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Works out the SQL Server connection string at application start, either from
    /// appsettings.json or from AWS Secrets Manager.
    /// </summary>
    public static class DatabaseConnectionResolver
    {
        /// <summary>
        /// Npgsql connection strings use Host=/Username=; SQL Server uses Server=/User Id=.
        /// Lets the UI report which engine is actually in use after a PostgreSQL migration.
        /// </summary>
        public static DatabaseProvider DetectProvider(string connectionString)
        {
            var looksPostgres = connectionString.IndexOf("Host=", StringComparison.OrdinalIgnoreCase) >= 0 && connectionString.IndexOf("Server=", StringComparison.OrdinalIgnoreCase) < 0;
            return looksPostgres ? DatabaseProvider.PostgreSql : DatabaseProvider.SqlServer;
        }
    }
}
