using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    // PORT-TODO
    // {"was": "DocumentDbContext.ConnectionString", "note": "Customer requires replacing the static ConnectionString property with constructor-injected DbContextOptions<DocumentDbContext> and proper DI registration. This requires adding Microsoft.EntityFrameworkCore.SqlServer to the .csproj, changing the base class to Microsoft.EntityFrameworkCore.DbContext, adding a constructor that takes DbContextOptions<DocumentDbContext>, removing the static ConnectionString property, and registering the DbContext in Program.cs with builder.Services.AddDbContext<DocumentDbContext>(). All callers that do 'new DocumentDbContext()' must be updated to receive it via DI instead.", "files": ["Program.cs"]}
    // PORT-TODO
    // {"note": "Customer requires adding an EF Core global query filter for soft-delete: in OnModelCreating, call modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted) so that manual !d.IsDeleted checks on every query can be removed. This requires EF Core (Microsoft.EntityFrameworkCore) — not possible with EF6 packages."}
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
        }
    }
}
