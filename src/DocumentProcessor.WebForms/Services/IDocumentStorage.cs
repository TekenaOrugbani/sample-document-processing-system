using System.IO;
using System.Threading.Tasks;

namespace DocumentProcessor.WebForms.Services
{
    public interface IDocumentStorage
    {
        /// <summary>
        /// Saves the upload and returns its path relative to the storage root. Asynchronous so
        /// that it accepts any stream, including streams that only support asynchronous reads.
        /// </summary>
        Task<string> SaveAsync(Stream content, string fileName);

        Stream OpenRead(string storagePath);
    }
}
