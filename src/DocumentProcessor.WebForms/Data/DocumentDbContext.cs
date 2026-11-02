using Microsoft.EntityFrameworkCore;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// EF Core DbContext for document storage. Register with
    /// <c>AddDbContext&lt;DocumentDbContext&gt;</c> in DI and supply the connection
    /// string via <see cref="DbContextOptionsBuilder.UseSqlServer(string)"/>.
    /// The schema is created externally (App_Data/Schema.sql), so no migrations are used.
    /// </summary>
    public class DocumentDbContext : DbContext
    {
        public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
    }
}
