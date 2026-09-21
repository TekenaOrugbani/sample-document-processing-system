using System;
using System.Diagnostics;
using System.Linq;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Models;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>Extracts text from a stored document and saves an AI-generated summary.</summary>
    public class DocumentPipeline
    {
        private readonly IDocumentStorage _storage;
        private readonly DocumentTextExtractor _textExtractor;
        private readonly IDocumentSummarizer _summarizer;

        public DocumentPipeline()
            : this(new FileSystemDocumentStorage(), new DocumentTextExtractor(), new BedrockDocumentSummarizer())
        {
        }

        public DocumentPipeline(
            IDocumentStorage storage,
            DocumentTextExtractor textExtractor,
            IDocumentSummarizer summarizer)
        {
            _storage = storage;
            _textExtractor = textExtractor;
            _summarizer = summarizer;
        }

        public void Process(Guid documentId)
        {
            using (var db = new DocumentDbContext())
            {
                // Entity Framework 6 has no global query filter, so every query has to
                // remember the soft-delete condition for itself.
                var document = db.Documents.FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);

                if (document == null)
                {
                    Trace.TraceWarning("Document {0} not found; skipping processing.", documentId);
                    return;
                }

                try
                {
                    document.Status = DocumentStatus.Processing;
                    db.SaveChanges();

                    using (var content = _storage.OpenRead(document.StoragePath))
                    {
                        var text = _textExtractor.Extract(document.FileExtension, content);

                        if (string.IsNullOrWhiteSpace(text))
                        {
                            throw new InvalidOperationException(
                                "No text could be extracted from the document.");
                        }

                        document.Summary = _summarizer.Summarize(document.OriginalFileName, text);
                    }

                    document.Status = DocumentStatus.Processed;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Processing failed for document {0}. {1}", documentId, ex);
                    document.Status = DocumentStatus.Failed;
                }

                db.SaveChanges();
            }
        }
    }
}
