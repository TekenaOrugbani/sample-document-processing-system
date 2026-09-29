using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        // PORT-TODO
        // {"was": "System.Data.Entity.Database.SetInitializer", "note": "EF6 Database.SetInitializer<DocumentDbContext>(null) was removed. If you need to suppress EF Core migrations, configure accordingly in AddDbContext or use Database.EnsureCreated(). The schema is created by App_Data/Schema.sql at application start.", "files": ["Program.cs"]}


        public DbSet<Document> Documents { get; set; }
    }
}
