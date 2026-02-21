using Core.Domain;

namespace Core.Abstractions
{
    public interface IPurchaseOrderRepository
    {
        IEnumerable<PurchaseOrderLine> GetLines(string partCode, string? currencyCode = null);
    }
}
