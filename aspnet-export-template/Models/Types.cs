using System.Text.Json.Serialization;

namespace AspNetExportTemplate.Models
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("_results")]
        public List<T>? Results { get; set; }

        [JsonPropertyName("_pagination")]
        public Pagination? Pagination { get; set; }
    }

    public class Pagination
    {
        [JsonPropertyName("next")]
        public string? Next { get; set; }
    }

    public class Resource
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    public class Inbox : Resource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("is_private")]
        public bool IsPrivate { get; set; }
    }

    public class Teammate : Resource
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class Recipient : Resource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("handle")]
        public string? Handle { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }

    public class Attachment
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class Message : Resource
    {
        [JsonPropertyName("created_at")]
        public double CreatedAt { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("blurb")]
        public string? Blurb { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("attachments")]
        public List<Attachment>? Attachments { get; set; }
    }

    public class Comment : Resource
    {
        [JsonPropertyName("posted_at")]
        public long PostedAt { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("attachments")]
        public List<Attachment>? Attachments { get; set; }
    }

    public class Conversation : Resource
    {
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("created_at")]
        public double CreatedAt { get; set; }
    }

    public class ExportOptions
    {
        public bool ShouldIncludeMessages { get; set; } = true;
        public bool ShouldIncludeComments { get; set; } = true;
        public bool ShouldIncludeAttachments { get; set; } = true;
    }
}
