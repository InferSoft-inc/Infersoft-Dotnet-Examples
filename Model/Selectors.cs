using System.Text.Json.Serialization;

namespace Examples.Model
{
    internal class Selectors
    {
        public Selector[] Exclude { get; set; } = Array.Empty<Selector>();
        
        public Selector[] Include { get; set; } = Array.Empty<Selector>();
    }
    
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(NameSelector), "nameSelector")]
    [JsonDerivedType(typeof(ProjectSelector), "projectSelector")]
    [JsonDerivedType(typeof(TagSelector), "tagSelector")]
    [JsonDerivedType(typeof(DocumentClassSelector), "documentClassSelector")]
    internal abstract class Selector
    {
        [JsonIgnore]
        public abstract string Type { get; }
    }

    internal sealed class NameSelector : Selector
    {
        [JsonIgnore]
        public override string Type => "nameSelector";
        
        public required string Name { get; set; }
    }

    internal sealed class ProjectSelector: Selector
    {
        [JsonIgnore]
        public override string Type => "projectSelector";

        public required int ProjectId { get; set; }
    }

    internal sealed class TagSelector : Selector
    {
        [JsonIgnore]
        public override string Type => "tagSelector";

        public required int[] Tags { get; set; }

        public DateTime? TaggedFrom { get; set; }
        public DateTime? TaggedTo { get; set; }
    }

    internal sealed class DocumentClassSelector : Selector
    {
        [JsonIgnore]
        public override string Type => "documentClassSelector";

        public required string[] Classes { get; set; }
    }
}
