using System;
using System.Linq;
using System.Threading.Tasks;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.WebForms.Services
{
    /// <summary>Extracts text from a stored document and saves an AI-generated summary.</summary>
    public class DocumentPipeline
    {
        private readonly IDocumentStorage _storage;
        private readonly DocumentTextExtractor _textExtractor;
        private readonly IDocumentSummarizer _summarizer;
        private readonly IDbContextFactory<DocumentDbContext> _dbFactory;
        private readonly ILogger<DocumentPipeline> _logger;

        public DocumentPipeline(
            IDocumentStorage storage,
            DocumentTextExtractor textExtractor,
            IDocumentSummarizer summarizer,
            IDbContextFactory<DocumentDbContext> dbFactory,
            ILogger<DocumentPipeline> logger)
        {
            _storage = storage;
            _textExtractor = textExtractor;
            _summarizer = summarizer;
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid documentId)
        {
            using (var db = await _dbFactory.CreateDbContextAsync())
            {
                // Entity Framework 6 has no global query filter, so every query has to
                // remember the soft-delete condition for itself.
                var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);

                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found; skipping processing.", documentId);
                    return;
                }

                try
                {
                    document.Status = DocumentStatus.Processing;
                    await db.SaveChangesAsync();

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
                    _logger.LogError(ex, "Processing failed for document {DocumentId}.", documentId);
                    document.Status = DocumentStatus.Failed;
                }

                await db.SaveChangesAsync();
            }
        }
    }
}
