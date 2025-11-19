using System.Text.Json.Serialization;

namespace Examples.Model
{
    internal class UploadResponse
    {
        public List<UploadItem> Items { get; set; } = new();
    }

    internal class UploadItem
    {
        public string ClientFileName { get; set; } = string.Empty;
        
        public string Error { get; set; } = string.Empty;
        
        public DateTime ExpiresAt { get; set; }
        
        public string Key { get; set; } = string.Empty;
        
        public string PutUrl { get; set; } = string.Empty;
        
        public Dictionary<string, string> RequiredHeaders { get; set; } = new();
        
        public string UploadMode { get; set; } = string.Empty;
    }
}
