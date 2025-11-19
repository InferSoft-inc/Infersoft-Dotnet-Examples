using System.Text.Json.Serialization;

namespace Examples.Model
{
    internal class EstimateCreditsResponse
    {
        public string Id { get; set; }
        
        public int PageCount { get; set; }
        
        public int TotalCredits { get; set; }
    }

    internal class EstimateCreditsRequest
    {
        public required int[] Prompts { get; set; }
        
        public required Selectors Selectors { get; set; }
        
        public required string[] Steps { get; set; }
        
        public bool Synchronous { get; set; }
    }
}
