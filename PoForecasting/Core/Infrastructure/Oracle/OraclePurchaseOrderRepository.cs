using Core.Abstractions;
using Core.Domain;
using Oracle.ManagedDataAccess.Client;

namespace Core.Infrastructure.Oracle
{
    public sealed class OraclePurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly string _connectionString;

        public OraclePurchaseOrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IEnumerable<PurchaseOrderLine> GetLines(string partCode, string? currencyCode = null)
        {
            if (string.IsNullOrWhiteSpace(partCode))
                yield break;

            var part = partCode.Trim();
            var curr = string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode.Trim();

            // Mirror CSV behavior:
            // - case-insensitive comparisons
            // - trim CHAR padding
            // - skip null/invalid price (CSV TryParseDecimal fails => skip)
            // - order by date like the CSV stream naturally does
            const string sql = @"SELECT ORDER_DATE, TRIM(PART_CODE) AS PART_CODE, TRIM(SYS_CURRENCY_CODE) AS SYS_CURRENCY_CODE, PRICE_PER_UNIT FROM PO_ORDER_ANALYSIS
                                WHERE UPPER(TRIM(PART_CODE)) = UPPER(:p_part)   AND (:p_curr IS NULL OR UPPER(TRIM(SYS_CURRENCY_CODE)) = UPPER(:p_curr)) ORDER BY ORDER_DATE";

            using var conn = new OracleConnection(_connectionString);
            conn.Open();

            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            cmd.Parameters.Add("p_part", OracleDbType.Varchar2).Value = part;
            cmd.Parameters.Add("p_curr", OracleDbType.Varchar2).Value = (object?)curr ?? DBNull.Value;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // ORDER_DATE is NOT NULL (per table definition)
                var orderDate = reader.GetDateTime(0);

                // PART_CODE, SYS_CURRENCY_CODE are NOT NULL in table definition,
                // but we keep safe checks anyway.
                var partRaw = reader.IsDBNull(1) ? part : reader.GetString(1).Trim();
                var currRaw = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim();

                // PRICE_PER_UNIT can be NULL in table definition => mimic CSV TryParseDecimal failure => skip
                if (reader.IsDBNull(3))
                    continue;

                decimal price;
                try
                {
                    price = reader.GetDecimal(3);
                }
                catch
                {
                    // Mimic "TryParseDecimal failed => skip"
                    continue;
                }

                // Extra safety: mimic CSV "partCode/currencyCode" checks in code layer too
                // (even though SQL already filters).
                if (!partRaw.Equals(partCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(currencyCode)
                    && !currRaw.Equals(currencyCode, StringComparison.OrdinalIgnoreCase))
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