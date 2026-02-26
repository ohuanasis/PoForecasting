using Core.Abstractions;
using Core.Domain;
using Oracle.ManagedDataAccess.Client;

namespace Core.Infrastructure.Oracle
{
    public sealed class OracleCpiRepository : ICpiRepository
    {
        private readonly string _connectionString;

        public OracleCpiRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IReadOnlyList<CpiPoint> GetMonthlyCpi()
        {
            var list = new List<CpiPoint>();

            // Mirror CSV behavior:
            // - read all rows
            // - normalize to yyyy-MM-01
            // - skip invalid/null CPI (CSV TryParseDecimal fails => skip)
            // - later group by month and pick highest CPI for duplicates
            const string sql = @"SELECT OBSERVATION_DATE, CPIAUCSL FROM CPI_DATA ORDER BY OBSERVATION_DATE";

            using var conn = new OracleConnection(_connectionString);
            conn.Open();

            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // OBSERVATION_DATE is PK (NOT NULL)
                var dt = reader.GetDateTime(0);

                if (reader.IsDBNull(1))
                    continue;

                decimal cpi;
                try
                {
                    cpi = reader.GetDecimal(1);
                }
                catch
                {
                    // Mimic "TryParseDecimal failed => skip"
                    continue;
                }

                var month = new DateTime(dt.Year, dt.Month, 1);

                list.Add(new CpiPoint
                {
                    Month = month,
                    Cpi = cpi
                });
            }

            // EXACTLY match CsvCpiRepository:
            return list
                .GroupBy(x => x.Month)
                .Select(g => g.OrderByDescending(x => x.Cpi).First())
                .OrderBy(x => x.Month)
                .ToList();
        }
    }
}