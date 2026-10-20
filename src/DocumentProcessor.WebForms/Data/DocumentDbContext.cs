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

        public DbSet<Document> Documents { get; set; }
    }
}
