using Core.Domain;

namespace Core.Services
{
    public static class MonthlySeriesBuilder
    {
        public static List<(DateTime Month, decimal AvgNominalPrice)> BuildMonthlyAvgPrice(IEnumerable<PurchaseOrderLine> lines)
        {
            return lines
                .GroupBy(x => new DateTime(x.OrderDate.Year, x.OrderDate.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => (Month: g.Key, AvgNominalPrice: g.Average(x => x.PricePerUnit)))
                .ToList();
        }

        public static Dictionary<DateTime, decimal> ToCpiMap(IReadOnlyList<CpiPoint> cpiPoints)
            => cpiPoints.ToDictionary(x => x.Month, x => x.Cpi);
    }
}
