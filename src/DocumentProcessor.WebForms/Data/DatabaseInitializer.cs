using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Creates the database and the Documents table on first run. Kept out of Entity
    /// Framework on purpose: a plain script is what the .NET 10 build of this application
    /// has to match, and it lets both builds point at the same database.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static async Task EnsureSchemaAsync(string connectionString, string schemaScriptPath)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            await CreateDatabaseIfMissingAsync(builder, databaseName);
            await RunSchemaScriptAsync(connectionString, schemaScriptPath);
        }

        private static async Task CreateDatabaseIfMissingAsync(SqlConnectionStringBuilder builder, string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                return;
            }

            // Reconnect to master; you cannot CREATE DATABASE from inside the database
            // you are trying to create.
            var master = new SqlConnectionStringBuilder(builder.ConnectionString)
            {
                InitialCatalog = "master"
            };
            const string sql =
                "IF DB_ID(@name) IS NULL " +
                "BEGIN " +
                "  DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); " +
                "  EXEC sp_executesql @sql; " +
                "END";
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@name", databaseName);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task RunSchemaScriptAsync(string connectionString, string scriptPath)
        {
            var script = await File.ReadAllTextAsync(scriptPath);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
