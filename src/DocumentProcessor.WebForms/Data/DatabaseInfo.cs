namespace DocumentProcessor.WebForms.Data
{
    public enum DatabaseProvider
    {
        SqlServer = 0,
        PostgreSql = 1
    }

    /// <summary>Where the app connected, surfaced in the UI banner and footer.</summary>
    public class DatabaseInfo
    {
        public DatabaseInfo(DatabaseProvider provider, string credentialSource, string hostAddress)
        {
            Provider = provider;
            CredentialSource = credentialSource;
            HostAddress = hostAddress;
        }

        public DatabaseProvider Provider { get; private set; }

        public string CredentialSource { get; private set; }

        public string HostAddress { get; private set; }

        public string DisplayName
        {
            get { return Provider == DatabaseProvider.PostgreSql ? "PostgreSQL" : "SQL Server"; }
        }
    }

    public class DatabaseConnection
    {
        public DatabaseConnection(string connectionString, DatabaseInfo info)
            : this(connectionString, info, null)
        {
        }

        public DatabaseConnection(string connectionString, DatabaseInfo info, string warning)
        {
            ConnectionString = connectionString;
            Info = info;
            Warning = warning;
        }

        public string ConnectionString { get; private set; }

        public DatabaseInfo Info { get; private set; }

        /// <summary>Non-fatal problem worth logging at startup; null when everything resolved cleanly.</summary>
        public string Warning { get; private set; }
    }
}
