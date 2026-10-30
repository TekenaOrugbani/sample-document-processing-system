using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using DocumentProcessor.WebForms.Controls;
using DocumentProcessor.WebForms.Data;
using DocumentProcessor.WebForms.Models;
using DocumentProcessor.WebForms.Services;

namespace DocumentProcessor.WebForms
{
    public partial class _Default : Page
    {
        private const int MaxDocumentsShown = 50;
        private const string DeleteTargetKey = "DeleteTargetId";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadDocuments();
            }
        }

        protected void Uploader_UploadRequested(object sender, UploadEventArgs e)
        {
            var storage = new FileSystemDocumentStorage();
            var pipeline = new DocumentPipeline();

            foreach (var file in e.Files)
            {
                var name = Path.GetFileName(file.FileName);

                try
                {
                    var document = Store(storage, file, name);

                    // Summarizing runs on the request thread, so the browser waits for
                    // Bedrock to answer before the page comes back.
                    pipeline.Process(document.Id);

                    Uploader.Notify(string.Format("'{0}' processed", name), NotificationLevel.Success);
                }
                catch (Exception ex)
                {
                    // System.Diagnostics.Trace has to be spelled out here: Page already has
                    // a Trace property of its own, and it is a TraceContext, not a logger.
                    System.Diagnostics.Trace.TraceError("Upload failed for {0}. {1}", name, ex);
                    Uploader.Notify(string.Format("'{0}' failed to upload", name), NotificationLevel.Danger);
                }
            }

            LoadDocuments();
        }

        protected void Documents_RefreshRequested(object sender, EventArgs e)
        {
            LoadDocuments();
        }

        protected void Documents_ViewSummaryRequested(object sender, DocumentEventArgs e)
        {
            var document = Find(e.DocumentId);

            if (document == null)
            {
                Uploader.Notify("That document is no longer available.", NotificationLevel.Warning);
                LoadDocuments();
                return;
            }

            SummaryModalTitle.Text = Server.HtmlEncode(document.OriginalFileName);
            SummaryModalBody.Text = Server.HtmlEncode(document.Summary ?? string.Empty);
            SummaryModal.Visible = true;
        }

        protected void CloseSummaryModal_Click(object sender, EventArgs e)
        {
            SummaryModal.Visible = false;
        }

        protected void Documents_DeleteRequested(object sender, DocumentEventArgs e)
        {
            var document = Find(e.DocumentId);

            if (document == null)
            {
                Uploader.Notify("That document is no longer available.", NotificationLevel.Warning);
                LoadDocuments();
                return;
            }

            ViewState[DeleteTargetKey] = document.Id;
            DeleteModalFileName.Text = Server.HtmlEncode(document.OriginalFileName);
            DeleteModal.Visible = true;
        }

        protected void CancelDelete_Click(object sender, EventArgs e)
        {
            ViewState.Remove(DeleteTargetKey);
            DeleteModal.Visible = false;
        }

        protected void ConfirmDelete_Click(object sender, EventArgs e)
        {
            if (!(ViewState[DeleteTargetKey] is Guid))
            {
                DeleteModal.Visible = false;
                return;
            }

            var documentId = (Guid)ViewState[DeleteTargetKey];

            try
            {
                // Soft delete: every query in this application filters IsDeleted out itself.
                using (var db = new DocumentDbContext())
                {
                    var document = db.Documents.FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);

                    if (document != null)
                    {
                        document.IsDeleted = true;
                        db.SaveChanges();
                    }
                }

                ViewState.Remove(DeleteTargetKey);
                DeleteModal.Visible = false;
                LoadDocuments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Delete failed for document {0}. {1}", documentId, ex);
                Uploader.Notify("Could not delete the document.", NotificationLevel.Danger);
            }
        }

        private void LoadDocuments()
        {
            try
            {
                using (var db = new DocumentDbContext())
                {
                    var documents = db.Documents
                        .Where(d => !d.IsDeleted)
                        .OrderByDescending(d => d.UploadedAt)
                        .Take(MaxDocumentsShown)
                        .ToList();

                    Documents.Bind(documents);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Could not load documents. {0}", ex);
                Documents.Bind(new List<Document>());
                Uploader.Notify("Could not load documents.", NotificationLevel.Danger);
            }
        }

        private static Document Find(Guid documentId)
        {
            using (var db = new DocumentDbContext())
            {
                return db.Documents.FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);
            }
        }

        private static Document Store(IDocumentStorage storage, HttpPostedFile file, string name)
        {
            var document = new Document
            {
                FileName = name,
                OriginalFileName = name,
                FileExtension = Path.GetExtension(name).ToLowerInvariant(),
                ContentType = file.ContentType,
                FileSize = file.ContentLength,
                UploadedAt = DateTimeOffset.UtcNow,
                Status = DocumentStatus.Pending
            };

            document.StoragePath = storage.Save(file.InputStream, name);

            using (var db = new DocumentDbContext())
            {
                db.Documents.Add(document);
                db.SaveChanges();
            }

            return document;
        }
    }
}
