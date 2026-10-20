using System;
using System.IO;
using DocumentProcessor.WebForms.Configuration;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>
    /// Stores uploads on the local file system under a date-partitioned path below
    /// the configured storage root directory.
    /// </summary>
    public class FileSystemDocumentStorage : IDocumentStorage
    {
        private readonly string _rootPath;

        /// <summary>
        /// Creates a new instance with the specified absolute root path.
        /// The root path is resolved by the DI container in Program.cs
        /// (replacing the legacy HostingEnvironment.MapPath call).
        /// </summary>
        public FileSystemDocumentStorage(string rootPath)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        }

        public string Save(Stream content, string fileName)
        {
            var now = DateTimeOffset.UtcNow;

            var uniqueName = Path.GetFileNameWithoutExtension(fileName)
                + "_" + now.ToString("yyyyMMddHHmmssfff")
                + Path.GetExtension(fileName);

            var relativePath = Path.Combine(
                now.Year.ToString("D4"),
                now.Month.ToString("D2"),
                now.Day.ToString("D2"),
                uniqueName);

            var fullPath = Path.Combine(_rootPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using (var file = File.Create(fullPath))
            {
                content.CopyTo(file);
            }

            return relativePath;
        }

        public Stream OpenRead(string storagePath)
        {
            return File.OpenRead(Path.Combine(_rootPath, storagePath));
        }
    }
}
