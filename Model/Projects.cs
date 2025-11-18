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
}
