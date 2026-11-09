using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    public class DocumentDbContext : DbContext
    {
        /// <summary>
        /// Set once at startup (Program.cs), because the connection string may come from
        /// Secrets Manager rather than appsettings.json and so is not known until the app starts.
        /// </summary>
        public static string ConnectionString { get; set; }

        public DocumentDbContext()
        {
        }

        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(ConnectionString))
            {
                optionsBuilder.UseSqlServer(ConnectionString);
            }
        }

        public DbSet<Document> Documents { get; set; }
    }
}
