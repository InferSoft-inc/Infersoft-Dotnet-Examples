namespace Examples.Model
{
    public class Selectors
    {
        public Selector[] Exclude { get; set; } = Array.Empty<Selector>();
        public Selector[] Include { get; set; } = Array.Empty<Selector>();
    }
    public abstract class Selector
    {
        public abstract string Type { get; }
    }

    public sealed class NameSelector : Selector
    {
        public override string Type => "nameSelector";
        public required string Name { get; set; }
    }

    public sealed class IdSelector : Selector
    {
        public override string Type => "idSelector";
        public required int Id { get; set; }
    }
}
