using AspNetExportTemplate.Data;
using AspNetExportTemplate.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AspNetExportTemplate.Services
{
    public class ExportService
    {
        private readonly IFrontConnector _connector;
        private readonly ExportDbContext _db;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly BlobContainerClient _container;

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

        public ExportService(IFrontConnector connector, ExportDbContext db)
        {
            _connector = connector;
            _db = db;
            _container = CreateContainerClient();
        }

        // Streamed search export: process each conversation as it arrives to minimize memory use
        public async Task<int> ExportSearchAsync(string searchText, long? after, List<string>? statuses, ExportOptions? options)
        {
            var query = BuildSearchQuery(searchText, after, statuses);
            var url = $"https://api2.frontapp.com/conversations/search/{Uri.EscapeDataString(query)}";
            var resolvedOptions = options ?? new ExportOptions();
            int exported = 0;
            await foreach (var conv in _connector.StreamPaginatedRequestAsync<Conversation>(url))
            {
                try
                {
                    await ExportConversationAsync(conv, "search", resolvedOptions);
                    exported++;
                }
                catch (Exception ex)
                {
                    await SaveFailedExportAsync(conv, "search", resolvedOptions, ex);
                }
            }
            return exported;
        }

        // Streamed inbox export: process conversations one-by-one as it arrives to minimize memory use
        public async Task<int> ExportInboxAsync(Inbox inbox, ExportOptions? options)
        {
            var url = $"https://api2.frontapp.com/inboxes/{inbox.Id}/conversations";
            var path = SanitizeFileName(inbox.Name ?? inbox.Id);
            var resolvedOptions = options ?? new ExportOptions();
            int exported = 0;
            await foreach (var conv in _connector.StreamPaginatedRequestAsync<Conversation>(url))
            {
                try
                {
                    await ExportConversationAsync(conv, path, resolvedOptions);
                    exported++;
                }
                catch (Exception ex)
                {
                    await SaveFailedExportAsync(conv, path, resolvedOptions, ex);
                }
            }
            return exported;
        }

        public async Task<(int succeeded, int failed)> RetryFailedExportsAsync()
        {
            var pending = await _db.FailedExports
                .Where(f => f.Status == FailedExportStatus.Pending)
                .ToListAsync();

            int succeeded = 0, failed = 0;
            foreach (var record in pending)
            {
                try
                {
                    var conv = JsonSerializer.Deserialize<Conversation>(record.ConversationJson, _jsonOptions)!;
                    var options = JsonSerializer.Deserialize<ExportOptions>(record.OptionsJson) ?? new ExportOptions();
                    await ExportConversationAsync(conv, record.ExportPath, options);
                    record.Status = FailedExportStatus.Succeeded;
                    record.RetryCount++;
                    succeeded++;
                }
                catch (Exception ex)
                {
                    record.RetryCount++;
                    record.LastError = ex.Message;
                    if (record.RetryCount >= 3)
                        record.Status = FailedExportStatus.PermanentlyFailed;
                    failed++;
                }
            }
            await _db.SaveChangesAsync();
            return (succeeded, failed);
        }

        public Task<List<FailedExport>> GetFailedExportsAsync()
        {
            return _db.FailedExports.Where(f => f.Status == FailedExportStatus.Pending).ToListAsync();
        }
        private async Task ExportConversationAsync(Conversation conv, string exportPath, ExportOptions options)
        {
            var convPath = Path.Combine(exportPath, conv.Id ?? Guid.NewGuid().ToString()).Replace("\\", "/");
            var convJson = JsonSerializer.Serialize(conv, _jsonOptions);
            await UploadStringAsync(Path.Combine(convPath, $"{conv.Id}.json").Replace("\\", "/"), convJson);

            if (options.ShouldIncludeMessages)
            {
                await foreach (var msg in _connector.StreamPaginatedRequestAsync<Message>($"https://api2.frontapp.com/conversations/{conv.Id}/messages"))
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
                await foreach (var c in _connector.StreamPaginatedRequestAsync<Comment>($"https://api2.frontapp.com/conversations/{conv.Id}/comments"))
                {
                    var commentFile = Path.Combine(convPath, $"{c.PostedAt}-comment-{c.Id}.json").Replace("\\", "/");
                    await UploadStringAsync(commentFile, JsonSerializer.Serialize(c, _jsonOptions));
                }
            }
        }

        private async Task SaveFailedExportAsync(Conversation conv, string exportPath, ExportOptions options, Exception ex)
        {
            var record = new FailedExport
            {
                ConversationId = conv.Id ?? string.Empty,
                ExportPath = exportPath,
                ConversationJson = JsonSerializer.Serialize(conv, _jsonOptions),
                OptionsJson = JsonSerializer.Serialize(options),
                FailedAt = DateTime.UtcNow,
                RetryCount = 0,
                LastError = ex.Message,
                Status = FailedExportStatus.Pending
            };
            _db.FailedExports.Add(record);
            await _db.SaveChangesAsync();
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

        /// <summary>
        /// Count unique conversations already exported for given inbox (based on blob prefix). Uses the blob path format: {inbox}/{conversationId}/...
        /// </summary>
        public async Task<int> CountExportedConversationsAsync(Inbox inbox)
        {
            var prefix = SanitizeFileName(inbox.Name ?? inbox.Id) + "/";
            var seen = new HashSet<string>();
            await foreach (var blobItem in _container.GetBlobsAsync(prefix: prefix))
            {
                var parts = blobItem.Name.Split('/');
                if (parts.Length >= 2) seen.Add(parts[1]);
            }
            return seen.Count;
        }

        private string BuildSearchQuery(string text, long? after, List<string>? statuses)
        {
            var parts = new List<string>();
            if (after.HasValue) parts.Add($"after:{after.Value}");
            if (statuses != null) parts.AddRange(statuses.Select(s => $"is:{s}"));
            parts.Add(text);
            return string.Join(' ', parts);
        }

        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}