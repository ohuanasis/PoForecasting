using Core.Abstractions;
using Core.Domain;

namespace Core.Infrastructure.Csv
{
    public sealed class CsvPurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly string _poCsvPath;

        public CsvPurchaseOrderRepository(string poCsvPath)
        {
            _poCsvPath = poCsvPath;
        }

        public IEnumerable<PurchaseOrderLine> GetLines(string partCode, string? currencyCode = null)
        {
            using var sr = new StreamReader(_poCsvPath);
            _ = sr.ReadLine(); // header

            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = CsvParsing.SplitLine(line);
                if (cols.Length < 10) continue;

                // PO CSV columns (from your example):
                // 1 ORDER_DATE
                // 4 PART_CODE
                // 7 PRICE_PER_UNIT
                // 9 SYS_CURRENCY_CODE
                var orderDateRaw = cols[1].Trim();
                var partRaw = cols[4].Trim();
                var priceRaw = cols[7].Trim();
                var currRaw = cols[9].Trim();

                if (!partRaw.Equals(partCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(currencyCode)
                    && !currRaw.Equals(currencyCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!CsvParsing.TryParseDate(orderDateRaw, out var orderDate))
                    continue;

                if (!CsvParsing.TryParseDecimal(priceRaw, out var price))
                    continue;

                yield return new PurchaseOrderLine
                {
                    OrderDate = orderDate,
                    PartCode = partRaw,
                    CurrencyCode = currRaw,
                    PricePerUnit = price
                };
            }
        }
    }
}
