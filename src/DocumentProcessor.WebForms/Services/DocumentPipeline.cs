using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>Extracts text from a stored document and saves an AI-generated summary.</summary>
    public class DocumentPipeline
    {
        private readonly DocumentDbContext _db;
        private readonly IDocumentStorage _storage;
        private readonly DocumentTextExtractor _textExtractor;
        private readonly IDocumentSummarizer _summarizer;

        public DocumentPipeline(
            DocumentDbContext db,
            IDocumentStorage storage,
            DocumentTextExtractor textExtractor,
            IDocumentSummarizer summarizer)
        {
            _db = db;
            _storage = storage;
            _textExtractor = textExtractor;
            _summarizer = summarizer;
        }

        public async Task ProcessAsync(Guid documentId)
        {
            var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                Trace.TraceWarning("Document {0} not found; skipping processing.", documentId);
                return;
            }

            try
            {
                document.Status = DocumentStatus.Processing;
                await _db.SaveChangesAsync();

                using (var content = _storage.OpenRead(document.StoragePath))
                {
                    var text = _textExtractor.Extract(document.FileExtension, content);

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidOperationException(
                            "No text could be extracted from the document.");
                    }

                    document.Summary = await _summarizer.SummarizeAsync(document.OriginalFileName, text);
                }

                document.Status = DocumentStatus.Processed;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Processing failed for document {0}. {1}", documentId, ex);
                document.Status = DocumentStatus.Failed;
            }

            await _db.SaveChangesAsync();
        }
    }
}
