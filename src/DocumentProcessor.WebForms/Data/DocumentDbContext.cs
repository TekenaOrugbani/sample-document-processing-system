using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Register via <c>AddDbContext&lt;DocumentDbContext&gt;</c> in DI.
    /// The schema is created by App_Data/Schema.sql at application start;
    /// EnsureCreated / migrations are not used.
    /// </summary>
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global query filter for the IsDeleted soft-delete pattern.
            modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        }
    }
}
