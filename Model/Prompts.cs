using System.Text.Json.Serialization;

namespace Examples.Model
{
    internal class PromptQueryRequest
    {
        public int Page { get; set; } = 1;
        
        public int PageSize { get; set; } = 50;
    }

    internal class PromptQueryResponse
    {
        public List<PromptMeta> Items { get; set; } = new();
        
        public int Page { get; set; }
        
        public int PageSize { get; set; }
        
        public bool HasMore { get; set; }
    }

    internal class PromptMeta
    {
        public int Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
        
        public string DocumentClass { get; set; } = string.Empty;
    }
}
