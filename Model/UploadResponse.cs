namespace Examples.Model
{
    internal class UploadResponse
    {
        public List<UploadItem> Items { get; set; } = new();
        public Project? Project { get; set; }
        public List<Tag> Tags { get; set; } = new();
    }

    internal class UploadItem
    {
        public string ClientFileName { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public UploadError? Error { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string PutUrl { get; set; } = string.Empty;
        public Dictionary<string, string> RequiredHeaders { get; set; } = new();
        public string UploadMode { get; set; } = string.Empty;
    }

    internal class UploadError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public long? MaxSize { get; set; }
        public bool? Retryable { get; set; }
    }

    internal class Project
    {
        public DateTime CreatedAt { get; set; }
        public bool Deleted { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
    }

    internal class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
