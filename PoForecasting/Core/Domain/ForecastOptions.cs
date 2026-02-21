using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Domain
{
    public sealed class ForecastOptions
    {
        // Recommended default: true
        public bool UseLogTransform { get; init; } = true;

        // Small positive number to avoid log(0). Use something tiny relative to your prices.
        public decimal LogEpsilon { get; init; } = 0.0001m;

        // Keep your existing rule: require at least 24 months
        public int MinMonthlyPoints { get; init; } = 24;

        // Confidence level (SSA)
        public float ConfidenceLevel { get; init; } = 0.95f;
    }
}
