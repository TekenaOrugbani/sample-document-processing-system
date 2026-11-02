using System;
using System.IO;
using System.Threading.Tasks;
using DocumentProcessor.WebForms.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>
    /// Stores uploads on the local file system under a date-partitioned path below
    /// the content root, which Kestrel will not serve directly.
    /// </summary>
    public class FileSystemDocumentStorage : IDocumentStorage
    {
        private readonly string _rootPath;

        public FileSystemDocumentStorage(AppSettings appSettings, IWebHostEnvironment environment)
        {
            var storagePath = appSettings.StorageRootPath;

            // The legacy setting was a virtual path "~/App_Data/uploads"; the modern
            // setting is a relative path like "uploads". Resolve it against ContentRootPath.
            if (storagePath.StartsWith("~/"))
            {
                storagePath = storagePath.Substring(2);
            }

            _rootPath = Path.Combine(environment.ContentRootPath, storagePath);
        }

        public async Task<string> SaveAsync(Stream content, string fileName)
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

            using (var file = File.Create(fullPath, 81920, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file);
            }

            return relativePath;
        }

        public Stream OpenRead(string storagePath)
        {
            return File.OpenRead(Path.Combine(_rootPath, storagePath));
        }
    }
}
