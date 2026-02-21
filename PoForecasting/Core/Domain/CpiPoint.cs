namespace Core.Domain
{
    public sealed class CpiPoint
    {
        public DateTime Month { get; init; } // first day of month
        public decimal Cpi { get; init; }
    }
}
