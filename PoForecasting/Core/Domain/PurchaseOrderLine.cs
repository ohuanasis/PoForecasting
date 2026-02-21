namespace Core.Domain
{
    public sealed class PurchaseOrderLine
    {
        public DateTime OrderDate { get; init; }
        public string PartCode { get; init; } = "";
        public string CurrencyCode { get; init; } = "";
        public decimal PricePerUnit { get; init; }
    }
}
