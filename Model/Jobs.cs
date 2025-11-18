namespace examples.Model
{
    internal class StartJobsRequest
    {
        public required string CreditsId { get; set; }
        public required int ProjectId { get; set; }
    }

    internal class StartJobsResponse
    {
        public int CompletedDocs { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Credits { get; set; }
        public int ErrorCount { get; set; }
        public int Id { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public Selectors Selectors { get; set; } = new();
        public string[] Stages { get; set; } = Array.Empty<string>();
        public string Status { get; set; } = string.Empty;
        public int TotalDocs { get; set; }
    }
}