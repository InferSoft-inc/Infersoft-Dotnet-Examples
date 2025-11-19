using System.Text.Json.Serialization;

namespace Examples.Model
{
    internal class UploadRequest
    {
        public List<UploadFile> Files { get; set; } = new();
    }

    internal class UploadFile
    {
        public string ContentType { get; set; } = string.Empty;
        
        public string FileName { get; set; } = string.Empty;
        
        public long Size { get; set; }
    }
}
