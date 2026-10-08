using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    public class DocumentDbContext : DbContext
    {
        /// <summary>
        /// Set once by Global.asax, because the connection string may come from Secrets
        /// Manager rather than Web.config and so is not known until the app starts.
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
            // When no options were injected (e.g. non-DI usage), fall back to the
            // static connection string that was set at application start.
            if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(ConnectionString))
            {
                optionsBuilder.UseSqlServer(ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global query filter replaces manual .Where(d => !d.IsDeleted) on every query.
            modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        }

        public DbSet<Document> Documents { get; set; }
    }
}
