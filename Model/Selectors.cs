namespace Examples.Model
{
    internal class Selectors
    {
        public Selector[] Exclude { get; set; } = Array.Empty<Selector>();
        public Selector[] Include { get; set; } = Array.Empty<Selector>();
    }
    internal abstract class Selector
    {
        public abstract string Type { get; }
    }

    internal sealed class NameSelector : Selector
    {
        public override string Type => "nameSelector";
        public required string Name { get; set; }
    }

    internal sealed class ProjectSelector: Selector
    {
        public override string Type => "projectSelector";
        public required int ProjectId { get; set; }
    }
}
