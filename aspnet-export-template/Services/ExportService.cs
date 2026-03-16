using AspNetExportTemplate.Models;
using System.Text.Json;

namespace AspNetExportTemplate.Services
{
    public class ExportService
    {
        private readonly IFrontConnector _connector;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public ExportService(IFrontConnector connector)
        {
            _connector = connector;
        }

        public async Task<List<Conversation>> ExportSearchAsync(string searchText, long? after, List<string>? statuses, ExportOptions? options)
        {
            var query = BuildSearchQuery(searchText, after, statuses);
            var url = $"https://api2.frontapp.com/conversations/search/{Uri.EscapeDataString(query)}";
            var conversations = await _connector.MakePaginatedRequestAsync<Conversation>(url);
            await ExportConversations(conversations, "./export/search", options ?? new ExportOptions());
            return conversations;
        }

        public async Task<List<Conversation>> ExportInboxAsync(Inbox inbox, ExportOptions? options)
        {
            var url = $"https://api2.frontapp.com/inboxes/{inbox.Id}/conversations";
            var conversations = await _connector.MakePaginatedRequestAsync<Conversation>(url);
            var path = $"./export/{SanitizeFileName(inbox.Name ?? inbox.Id)}";
            Directory.CreateDirectory(path);
            await ExportConversations(conversations, path, options ?? new ExportOptions());
            return conversations;
        }

        private async Task ExportConversations(List<Conversation> conversations, string exportPath, ExportOptions options)
        {
            foreach (var conv in conversations)
            {
                var convPath = Path.Combine(exportPath, conv.Id ?? Guid.NewGuid().ToString());
                Directory.CreateDirectory(convPath);
                await File.WriteAllTextAsync(Path.Combine(convPath, $"{conv.Id}.json"), JsonSerializer.Serialize(conv, _jsonOptions));

                if (options.ShouldIncludeMessages)
                {
                    var messages = await _connector.MakePaginatedRequestAsync<Message>($"https://api2.frontapp.com/conversations/{conv.Id}/messages");
                    foreach (var msg in messages)
                    {
                        var messageFile = Path.Combine(convPath, $"{msg.CreatedAt}-message-{msg.Id}.json");
                        await File.WriteAllTextAsync(messageFile, JsonSerializer.Serialize(msg, _jsonOptions));

                        if (options.ShouldIncludeAttachments && msg.Attachments != null)
                        {
                            var attachmentsPath = Path.Combine(convPath, "attachments", msg.Id ?? Guid.NewGuid().ToString());
                            Directory.CreateDirectory(attachmentsPath);
                            foreach (var at in msg.Attachments)
                            {
                                var data = await _connector.GetAttachmentAsync(at.Url!);
                                if (data != null)
                                {
                                    var fileName = string.IsNullOrWhiteSpace(at.Filename)
                                        //? (at.Id ?? Guid.NewGuid().ToString()) // TODO: Add this line instead of below line if necessary
                                        ? at.Id
                                        : at.Filename;

                                    await File.WriteAllBytesAsync(Path.Combine(attachmentsPath, fileName), data);
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
                        var commentFile = Path.Combine(convPath, $"{c.PostedAt}-comment-{c.Id}.json");
                        await File.WriteAllTextAsync(commentFile, JsonSerializer.Serialize(c, _jsonOptions));
                    }
                }
            }
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
