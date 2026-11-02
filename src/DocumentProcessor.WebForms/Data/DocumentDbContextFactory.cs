using System;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Creates <see cref="DocumentDbContext"/> instances for short-lived unit-of-work
    /// patterns (used by <see cref="DocumentProcessor.WebForms.Services.DocumentPipeline"/>).
    /// Registered as a singleton in DI with the resolved connection string.
    /// </summary>
    public class DocumentDbContextFactory
    {
        private readonly string _connectionString;

        public DocumentDbContextFactory(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("A connection string is required.", nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public DocumentDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<DocumentDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            return new DocumentDbContext(options);
        }
    }
}
