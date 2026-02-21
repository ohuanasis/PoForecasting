using Core.Abstractions;
using Core.Domain;

namespace Core.Infrastructure.Csv
{
    public sealed class CsvCpiRepository : ICpiRepository
    {
        private readonly string _cpiCsvPath;

        public CsvCpiRepository(string cpiCsvPath)
        {
            _cpiCsvPath = cpiCsvPath;
        }

        public IReadOnlyList<CpiPoint> GetMonthlyCpi()
        {
            var list = new List<CpiPoint>();

            using var sr = new StreamReader(_cpiCsvPath);
            _ = sr.ReadLine(); // header: observation_date,CPIAUCSL

            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = CsvParsing.SplitLine(line);
                if (cols.Length < 2) continue;

                var dateRaw = cols[0].Trim();
                var cpiRaw = cols[1].Trim();

                if (!CsvParsing.TryParseDate(dateRaw, out var dt))
                    continue;

                if (!CsvParsing.TryParseDecimal(cpiRaw, out var cpi))
                    continue;

                var month = new DateTime(dt.Year, dt.Month, 1);

                list.Add(new CpiPoint { Month = month, Cpi = cpi });
            }

            return list
                .GroupBy(x => x.Month)
                .Select(g => g.OrderByDescending(x => x.Cpi).First())
                .OrderBy(x => x.Month)
                .ToList();
        }
    }
}
