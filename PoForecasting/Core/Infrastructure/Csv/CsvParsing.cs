using System.Globalization;

namespace Core.Infrastructure.Csv
{
    internal static class CsvParsing
    {
        public static bool TryParseDate(string raw, out DateTime dt)
        {
            raw = raw.Trim();
            string[] formats = { "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "yyyy-MM-dd" };

            return DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
                   || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
                   || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
        }

        public static bool TryParseDecimal(string raw, out decimal value)
        {
            raw = raw.Trim();
            return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        // Assumes no quoted commas (matches your sample). If your real data has quoted commas, we’ll upgrade this.
        public static string[] SplitLine(string line) => line.Split(',');
    }
}
