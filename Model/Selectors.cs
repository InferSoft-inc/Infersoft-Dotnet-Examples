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
}
