using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Seq.App.Metrics;

public static class Metrics
{
    private static readonly MeterProvider MeterProvider;
    private static readonly Meter Meter = new("Seq.App.Metrics");

    static Metrics()
    {
        MeterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("Seq.App.Metrics", autoGenerateServiceInstanceId: false))
            .AddMeter("Seq.App.Metrics")
            .AddOtlpExporter()
            .Build();
    }

    public static Counter<long> CreateCounter(string metricName, string? unit, string? description) =>
        Meter.CreateCounter<long>(metricName, unit, description);

    public static UpDownCounter<long> CreateUpDownCounter(string metricName, string? unit, string? description) =>
        Meter.CreateUpDownCounter<long>(metricName, unit, description);

    public static ObservableGauge<long> CreateGauge(string metricName, Func<IEnumerable<Measurement<long>>> getMeasurements, string? unit, string? description) => 
        Meter.CreateObservableGauge(metricName, getMeasurements, unit, description);

    public static Histogram<long> CreateHistogram(string metricName, string? unit, string? description) =>
        Meter.CreateHistogram<long>(metricName, unit, description);

    public static void AppDisposed() => MeterProvider.ForceFlush();
}