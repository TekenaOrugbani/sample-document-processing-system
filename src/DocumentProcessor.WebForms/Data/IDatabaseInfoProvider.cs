namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Provides the resolved <see cref="DatabaseInfo"/> to components via DI.
    /// Replaces the legacy <c>HttpContext.Current.Application[DatabaseInfoKey]</c> pattern.
    /// Register as a singleton in Program.cs and populate after
    /// <see cref="DatabaseConnectionResolver.Resolve"/> completes.
    /// </summary>
    public interface IDatabaseInfoProvider
    {
        /// <summary>
        /// The database connection metadata resolved at startup, or <c>null</c>
        /// if resolution has not yet completed or failed.
        /// </summary>
        DatabaseInfo? Info { get; }
    }

    /// <summary>
    /// Default mutable implementation populated once during application startup.
    /// </summary>
    public class DatabaseInfoProvider : IDatabaseInfoProvider
    {
        public DatabaseInfo? Info { get; set; }
    }
}
