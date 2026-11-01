using System.Data.Entity;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Create instances through <see cref="DocumentDbContextFactory"/>, which holds the
    /// connection string resolved at application start.
    /// </summary>
    public class DocumentDbContext : DbContext
    {
        static DocumentDbContext()
        {
            // The schema is created by App_Data/Schema.sql at application start, so EF6 must
            // not try to create the database or check its model hash against __MigrationHistory.
            Database.SetInitializer<DocumentDbContext>(null);
        }

        public DocumentDbContext(string connectionString)
            : base(connectionString)
        {
        }

        public DbSet<Document> Documents { get; set; }
    }
}
