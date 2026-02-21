using Core.Domain;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace Core.Services
{
    public sealed class SsaForecaster
    {
        private readonly MLContext _ml;

        public SsaForecaster(int seed = 1) => _ml = new MLContext(seed);

        private sealed class Point { public float Value { get; set; } }

        private sealed class Forecast
        {
            [VectorType] public float[] Forecasted { get; set; } = Array.Empty<float>();
            [VectorType] public float[] Lower { get; set; } = Array.Empty<float>();
            [VectorType] public float[] Upper { get; set; } = Array.Empty<float>();
        }

        public (decimal[] forecast, decimal[] lower, decimal[] upper, SsaParams p) ForecastSeries(
            IReadOnlyList<decimal> series, int horizon, float confidenceLevel = 0.95f)
        {
            if (series.Count < 24)
                throw new InvalidOperationException($"Need at least ~24 points. Got {series.Count}.");

            int trainSize = series.Count;
            int windowSize = Math.Min(12, Math.Max(4, trainSize / 6));
            int seriesLength = Math.Min(trainSize, windowSize * 2);

            var data = _ml.Data.LoadFromEnumerable(series.Select(v => new Point { Value = (float)v }));

            var pipeline = _ml.Forecasting.ForecastBySsa(
                outputColumnName: nameof(Forecast.Forecasted),
                inputColumnName: nameof(Point.Value),
                windowSize: windowSize,
                seriesLength: seriesLength,
                trainSize: trainSize,
                horizon: horizon,
                confidenceLevel: confidenceLevel,
                confidenceLowerBoundColumn: nameof(Forecast.Lower),
                confidenceUpperBoundColumn: nameof(Forecast.Upper));

            var model = pipeline.Fit(data);
            var engine = model.CreateTimeSeriesEngine<Point, Forecast>(_ml);
            var pred = engine.Predict();

            decimal[] f = pred.Forecasted.Select(x => (decimal)x).ToArray();
            decimal[] lo = pred.Lower.Select(x => (decimal)x).ToArray();
            decimal[] hi = pred.Upper.Select(x => (decimal)x).ToArray();

            return (f, lo, hi, new SsaParams
            {
                TrainSize = trainSize,
                WindowSize = windowSize,
                SeriesLength = seriesLength,
                Horizon = horizon
            });
        }

    }
}
