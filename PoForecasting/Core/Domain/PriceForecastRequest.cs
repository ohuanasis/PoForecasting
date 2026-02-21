using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Domain
{
    public sealed class PriceForecastRequest
    {
        public string PartCode { get; init; } = "";
        public int Months { get; init; } = 6;
        public string? CurrencyCode { get; init; }
        public ForecastOptions Options { get; init; } = new();
    }
}
