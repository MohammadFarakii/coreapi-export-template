namespace AspNetExportTemplate.Models
{
    public class FailedExport
    {
        public int Id { get; set; }
        public string ConversationId { get; set; } = string.Empty;
        public string ExportPath { get; set; } = string.Empty;
        public string ConversationJson { get; set; } = string.Empty;
        public string OptionsJson { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public FailedExportStatus Status { get; set; }
    }

    public enum FailedExportStatus
    {
        Pending = 0,
        Succeeded = 1,
        PermanentlyFailed = 2
    }
}
