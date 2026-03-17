using AspNetExportTemplate.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using System.IO;

namespace AspNetExportTemplate.Services
{
    public class ExportService
    {
        private readonly IFrontConnector _connector;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly BlobContainerClient _container;

        // Reads AZURE_STORAGE_CONNECTION_STRING and EXPORT_CONTAINER from env
        private static BlobContainerClient CreateContainerClient()
        {
            var conn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            var container = Environment.GetEnvironmentVariable("EXPORT_CONTAINER") ?? "front-exports";
            if (string.IsNullOrEmpty(conn))
            {
                throw new InvalidOperationException("Environment variable AZURE_STORAGE_CONNECTION_STRING must be set to upload exports to Azure Blob Storage.");
            }
            var client = new BlobContainerClient(conn, container);
            client.CreateIfNotExists(PublicAccessType.None);
            return client;
        }

        public ExportService(IFrontConnector connector)
        {
            _connector = connector;
            _container = CreateContainerClient();
        }

        public async Task<List<Conversation>> ExportSearchAsync(string searchText, long? after, List<string>? statuses, ExportOptions? options)
        {
            var query = BuildSearchQuery(searchText, after, statuses);
            var url = $"https://api2.frontapp.com/conversations/search/{Uri.EscapeDataString(query)}";
            var conversations = await _connector.MakePaginatedRequestAsync<Conversation>(url);
            await ExportConversations(conversations, "search", options ?? new ExportOptions());
            return conversations;
        }

        public async Task<List<Conversation>> ExportInboxAsync(Inbox inbox, ExportOptions? options)
        {
            var url = $"https://api2.frontapp.com/inboxes/{inbox.Id}/conversations";
            var conversations = await _connector.MakePaginatedRequestAsync<Conversation>(url);
            var path = SanitizeFileName(inbox.Name ?? inbox.Id);
            await ExportConversations(conversations, path, options ?? new ExportOptions());
            return conversations;
        }

        private async Task ExportConversations(List<Conversation> conversations, string exportPath, ExportOptions options)
        {
            foreach (var conv in conversations)
            {
                var convPath = Path.Combine(exportPath, conv.Id ?? Guid.NewGuid().ToString()).Replace("\\", "/");
                var convJson = JsonSerializer.Serialize(conv, options: _jsonOptions);
                await UploadStringAsync(Path.Combine(convPath, $"{conv.Id}.json").Replace("\\", "/"), convJson);

                if (options.ShouldIncludeMessages)
                {
                    var messages = await _connector.MakePaginatedRequestAsync<Message>($"https://api2.frontapp.com/conversations/{conv.Id}/messages");
                    foreach (var msg in messages)
                    {
                        var messageFile = Path.Combine(convPath, $"{msg.CreatedAt}-message-{msg.Id}.json").Replace("\\", "/");
                        await UploadStringAsync(messageFile, JsonSerializer.Serialize(msg, _jsonOptions));

                        if (options.ShouldIncludeAttachments && msg.Attachments != null)
                        {
                            var attachmentsPath = Path.Combine(convPath, "attachments", msg.Id ?? Guid.NewGuid().ToString()).Replace("\\", "/");
                            foreach (var at in msg.Attachments)
                            {
                                var data = await _connector.GetAttachmentAsync(at.Url!);
                                if (data != null)
                                {
                                    var fileName = string.IsNullOrWhiteSpace(at.Filename) ? at.Id : at.Filename;
                                    await UploadBytesAsync(Path.Combine(attachmentsPath, fileName).Replace("\\", "/"), data);
                                }
                            }
                        }
                    }
                }

                if (options.ShouldIncludeComments)
                {
                    var comments = await _connector.MakePaginatedRequestAsync<Comment>($"https://api2.frontapp.com/conversations/{conv.Id}/comments");
                    foreach (var c in comments)
                    {
                        var commentFile = Path.Combine(convPath, $"{c.PostedAt}-comment-{c.Id}.json").Replace("\\", "/");
                        await UploadStringAsync(commentFile, JsonSerializer.Serialize(c, _jsonOptions));
                    }
                }
            }
        }

        private async Task UploadStringAsync(string blobPath, string content)
        {
            var blobClient = _container.GetBlobClient(blobPath);
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(ms, overwrite: true);
        }

        private async Task UploadBytesAsync(string blobPath, byte[] data)
        {
            var blobClient = _container.GetBlobClient(blobPath);
            using var ms = new MemoryStream(data);
            await blobClient.UploadAsync(ms, overwrite: true);
        }

        private string BuildSearchQuery(string text, long? after, List<string>? statuses)
        {
            var parts = new List<string>();
            if (after.HasValue)
            {
                parts.Add($"after:{after.Value}");
            }
            if (statuses != null)
            {
                parts.AddRange(statuses.Select(s => $"is:{s}"));
            }
            parts.Add(text);
            return string.Join(' ', parts);
        }

        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
