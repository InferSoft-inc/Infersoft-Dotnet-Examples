namespace Examples.Model
{
    internal class UploadRequest
    {
        public List<UploadFile> Files { get; set; } = new();
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public List<int>? TagIds { get; set; }
        public List<string>? TagNames { get; set; }

    }

    internal class UploadFile
    {
        public string ContentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public long Size { get; set; }
    }
}
