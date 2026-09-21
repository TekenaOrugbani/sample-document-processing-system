namespace DocumentProcessor.Web.Data;

public enum DatabaseProvider { SqlServer, PostgreSql }

/// <summary>Where the app connected, surfaced in the UI banner and footer.</summary>
public sealed record DatabaseInfo(
    DatabaseProvider Provider,
    string CredentialSource,
    string HostAddress)
{
    public string DisplayName => Provider switch
    {
        DatabaseProvider.PostgreSql => "PostgreSQL",
        _ => "SQL Server"
    };
}

public sealed record DatabaseConnection(string ConnectionString, DatabaseInfo Info, string? Warning = null);
