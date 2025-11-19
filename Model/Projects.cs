namespace Examples.Model
{
    internal class CreateProjectRequest
    {
        public required string Name { get; set; }
    }

    internal class CreateProjectResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public bool Deleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    internal class AssignDocumentsRequest
    {
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public required Selectors Selectors { get; set; }
    }

    internal class AssignDocumentsResponse
    {
        public required ProjectInfo Project { get; set; }
        public int Matched { get; set; }
        public int Added { get; set; }
        public int Skipped { get; set; }
    }

    internal class ProjectInfo
    {
        public int Id { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Deleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
