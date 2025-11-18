namespace examples.Model





{
    using System;
    using System.Collections.Generic;

    public class DocumentsSearchRequest
    {
        public string OrderBy { get; set; }
        public string OrderDir { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public Selectors Selectors { get; set; }
    }

    internal class DocumentsSearchResponse
    {
        public bool HasMore { get; set; }
        public List<DocumentItem> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
    public class DocumentListResponse

    internal class DocumentItem
    {
        public DateTime CreatedAt { get; set; }
        public string DocumentClass { get; set; }
        public long FileSize { get; set; }
        public bool HasActiveWorkflow { get; set; }
        public int Id { get; set; }
        public bool IsValid { get; set; }
        public string Name { get; set; }
        public string OrganizationId { get; set; }
        public int PageCount { get; set; }
        public int SourceDocument { get; set; }
    }

    internal class ExtractionResponse
    {
        public bool HasMore { get; set; }
        public List<ExtractionItem> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    internal class ExtractionItem
    {
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> ExtractionBody { get; set; }
        public DateTime ExtractionCreatedAt { get; set; }
        public int ExtractionResultId { get; set; }
        public long FileSize { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string OrganizationId { get; set; }
        public int PageCount { get; set; }
        public int SourceDocument { get; set; }
    }
}