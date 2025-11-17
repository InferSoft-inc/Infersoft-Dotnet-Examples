
namespace examples.Model
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
        // Using NameSelector as an example; your actual implementatio should be more generic
        // since we leverage different selector types and so should you.
        public required NameSelector[] Selectors { get; set; }
        public required string[] Steps { get; set; }
        public bool Synchronous { get; set; }
    }
}
