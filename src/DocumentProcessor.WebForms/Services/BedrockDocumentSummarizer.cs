using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using DocumentProcessor.WebForms.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.WebForms.Services
{
    public class BedrockDocumentSummarizer : IDocumentSummarizer
    {
        private const string SystemPrompt =
            "Summarize the document in under 500 characters. Reply with the summary only.";

        private readonly IAmazonBedrockRuntime _client;
        private readonly AppSettings _settings;
        private readonly ILogger<BedrockDocumentSummarizer> _logger;

        public BedrockDocumentSummarizer(
            IAmazonBedrockRuntime client,
            IOptions<AppSettings> settings,
            ILogger<BedrockDocumentSummarizer> logger)
        {
            _client = client;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string> SummarizeAsync(string fileName, string text)
        {
            var request = new ConverseRequest
            {
                ModelId = _settings.BedrockSummarizationModelId,
                System = new List<SystemContentBlock>
                {
                    new SystemContentBlock { Text = SystemPrompt }
                },
                Messages = new List<Message>
                {
                    new Message
                    {
                        Role = ConversationRole.User,
                        Content = new List<ContentBlock>
                        {
                            new ContentBlock { Text = "File: " + fileName + "\n\n" + text }
                        }
                    }
                },
                // Claude Sonnet 5 rejects Temperature and TopP.
                InferenceConfig = new InferenceConfiguration
                {
                    MaxTokens = _settings.BedrockMaxTokens
                }
            };

            var response = await _client.ConverseAsync(request);

            if (response.Output == null ||
                response.Output.Message == null ||
                response.Output.Message.Content == null ||
                response.Output.Message.Content.Count == 0)
            {
                _logger.LogWarning(
                    "Bedrock returned no content for '{FileName}'. StopReason={StopReason}, HTTP={StatusCode}.",
                    fileName, response.StopReason, response.HttpStatusCode);

                return string.Empty;
            }

            // Claude Sonnet 5 can put a reasoning block ahead of the answer, so the first
            // block is not reliably the summary. Take the first one that carries text.
            foreach (var block in response.Output.Message.Content)
            {
                if (!string.IsNullOrWhiteSpace(block.Text))
                {
                    return block.Text.Trim();
                }
            }

            _logger.LogWarning(
                "Bedrock returned {Count} content block(s) but no text for '{FileName}'. StopReason={StopReason}.",
                response.Output.Message.Content.Count, fileName, response.StopReason);

            return string.Empty;
        }
    }
}
