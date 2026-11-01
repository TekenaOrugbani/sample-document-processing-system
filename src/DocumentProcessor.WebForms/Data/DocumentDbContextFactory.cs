using System;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// Creates short-lived <see cref="DocumentDbContext"/> instances for a unit of work.
    /// In the legacy app, Global.asax built this once with the resolved connection string
    /// and stored it in HttpApplication.Application state. Now it is registered in DI
    /// and uses <see cref="DbContextOptions{TContext}"/> built from the connection string
    /// configured via <c>AddDbContext</c>.
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
            var optionsBuilder = new DbContextOptionsBuilder<DocumentDbContext>();
            optionsBuilder.UseSqlServer(_connectionString);
            return new DocumentDbContext(optionsBuilder.Options);
        }
    }
}
