namespace Examples.Model
{
    public class UploadRequest
    {
        public List<UploadFile> Files { get; set; } = new();
    }

    public class UploadFile
    {
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}