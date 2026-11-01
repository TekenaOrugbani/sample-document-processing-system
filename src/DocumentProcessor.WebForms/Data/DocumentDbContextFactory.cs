using System;

namespace DocumentProcessor.WebForms.Data
{
    /// <summary>
    /// The one place that creates <see cref="DocumentDbContext"/> instances. Global.asax builds
    /// it once with the resolved connection string, because that string may come from Secrets
    /// Manager rather than Web.config and so is not known until the app starts. Pages and
    /// services ask it for a short-lived context per unit of work and dispose it afterwards.
    /// </summary>
    public class DocumentDbContextFactory
    {
        private readonly string _connectionString;

        public DocumentDbContextFactory(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("A connection string is required.", "connectionString");
            }

            _connectionString = connectionString;
        }

        public DocumentDbContext CreateDbContext()
        {
            return new DocumentDbContext(_connectionString);
        }
    }
}
