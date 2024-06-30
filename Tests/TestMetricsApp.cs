using OpenTelemetry.Metrics;
using Seq.App.OTelMetrics;

namespace Tests;

public class TestMetricsApp : MetricsApp
{
    protected override MeterProviderBuilder ConfigureExporters(MeterProviderBuilder meterProviderBuilder)
    {
        return meterProviderBuilder.AddInMemoryExporter(Metrics);
    }

    public void Flush()
    {
        MeterProvider?.ForceFlush();
    }

    public HashSet<Metric> Metrics { get; } = [];
}