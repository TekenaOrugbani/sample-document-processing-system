using System.Threading.Tasks;

namespace DocumentProcessor.WebForms.Services
{
    public interface IDocumentSummarizer
    {
        string Summarize(string fileName, string text);

        Task<string> SummarizeAsync(string fileName, string text);
    }
}
