using System;
using System.IO;
using DocumentProcessor.WebForms.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>
    /// Stores uploads on the local file system under a date-partitioned path below
    /// the content root, which Kestrel will not serve directly.
    /// </summary>
    public class FileSystemDocumentStorage : IDocumentStorage
    {
        private readonly string _rootPath;

        public FileSystemDocumentStorage(IWebHostEnvironment env, IOptions<AppSettings> settings)
        {
            var storagePath = settings.Value.StorageRootPath;

            // Strip legacy "~/" virtual-path prefix if present.
            if (storagePath.StartsWith("~/"))
            {
                storagePath = storagePath.Substring(2);
            }

            _rootPath = Path.Combine(env.ContentRootPath, storagePath);
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

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

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
