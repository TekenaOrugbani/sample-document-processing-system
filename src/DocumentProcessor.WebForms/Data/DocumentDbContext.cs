using System.Data.Entity;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    public class DocumentDbContext : DbContext
    {
        static DocumentDbContext()
        {
            // The schema is created by App_Data/Schema.sql at application start, so EF6 must
            // not try to create the database or check its model hash against __MigrationHistory.
            Database.SetInitializer<DocumentDbContext>(null);
        }

        /// <summary>
        /// Set once by Global.asax, because the connection string may come from Secrets
        /// Manager rather than Web.config and so is not known until the app starts.
        /// </summary>
        public static string ConnectionString { get; set; }

        public DocumentDbContext()
            : base(ConnectionString)
        {
        }

        public DbSet<Document> Documents { get; set; }
    }
}
