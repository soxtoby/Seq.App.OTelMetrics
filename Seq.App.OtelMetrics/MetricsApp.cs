using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.OTelMetrics;

[SeqApp("OTel Metrics", Description = "Collect metrics from log events and send them to an OpenTelemetry receiver.")]
public class MetricsApp : SeqApp, ISubscribeTo<LogEventData>, IDisposable
{
    protected MeterProvider? MeterProvider;
    private Meter? _meter;
    private Counter<long>? _counter;
    private UpDownCounter<long>? _upDownCounter;
    private readonly ConcurrentDictionary<string, Measurement<long>> _gaugeMeasurements = new();
    private Histogram<long>? _histogram;
    private string[] _includedProperties = [];

    [SeqAppSetting(DisplayName = "Type of metric")]
    public MetricType MetricType { get; set; }

    [SeqAppSetting(
        DisplayName = "Unit",
        HelpText = "Unit of measurement the metric represents.",
        IsOptional = true
    )]
    public string? Unit { get; set; }

    [SeqAppSetting(
        DisplayName = "Metric name",
        HelpText = "Customize the name of the metric to expose. If not specified, the title of the app instance will be used.",
        IsOptional = true
    )]
    public string? CustomMetricName { get; set; }

    [SeqAppSetting(
        DisplayName = "Metric description",
        IsOptional = true
    )]
    public string? Description { get; set; }

    [SeqAppSetting(
        DisplayName = "Value property",
        HelpText = "Property to take the metric value from. If not provided, counters will be incremented by 1 for each event, and histograms will use the span duration. Must be specified for other types of metrics.",
        IsOptional = true
    )]
    public string? ValueProperty { get; set; }

    [SeqAppSetting(
        DisplayName = "Included properties",
        HelpText = "Comma-separated list of properties to include as tags.",
        IsOptional = true
    )]
    public string? IncludedProperties { get; set; }

    [SeqAppSetting(
        DisplayName = "OTLP endpoint",
        HelpText = "The endpoint to send metrics to. If not provided, metrics will be sent to the local collector.",
        IsOptional = true
    )]
    public string? OtlpEndpoint { get; set; }

    [SeqAppSetting(
        DisplayName = "OTLP export protocol",
        HelpText = "Defaults to gRPC.",
        IsOptional = true
    )]
    public OtlpExportProtocol OtlpExportProtocol { get; set; }

    protected override void OnAttached()
    {
        MeterProvider = ConfigureExporters(Sdk.CreateMeterProviderBuilder()
                .ConfigureResource(r => r.AddService("Seq.App.OTelMetrics", autoGenerateServiceInstanceId: false))
                .AddMeter(App.Id))
            .Build();
        _meter = new Meter(App.Id);
        _includedProperties = IncludedProperties?.Split(',').Select(p => p.Trim()).ToArray() ?? [];

        switch (MetricType)
        {
            case MetricType.Counter:
                _counter = _meter.CreateCounter<long>(MetricName, Unit, Description);
                break;
            case MetricType.UpDownCounter:
                _upDownCounter = _meter.CreateUpDownCounter<long>(MetricName, Unit, Description);
                break;
            case MetricType.Gauge:
                _meter.CreateObservableGauge(MetricName, () => _gaugeMeasurements.Values, Unit, Description);
                break;
            case MetricType.Histogram:
                _histogram = _meter.CreateHistogram<long>(MetricName, Unit, Description);
                break;
            default:
                Log.Fatal("Unexpected metric type {MetricType}", MetricType);
                break;
        }
    }

    protected virtual MeterProviderBuilder ConfigureExporters(MeterProviderBuilder meterProviderBuilder)
    {
        return meterProviderBuilder.AddOtlpExporter(o =>
            {
                o.Protocol = OtlpExportProtocol;
                if (OtlpEndpoint is not null && Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri))
                    o.Endpoint = uri;
            });
    }

    private string MetricName => string.IsNullOrWhiteSpace(CustomMetricName) ? App.Title : CustomMetricName;

    public void On(Event<LogEventData> evt)
    {
        switch (MetricType)
        {
            case MetricType.Counter:
                AddToCounter(evt);
                break;
            case MetricType.UpDownCounter:
                AddToUpDownCounter(evt);
                break;
            case MetricType.Gauge:
                UpdateGauge(MetricValueFromEvent(evt) ?? 0, TagsFromEvent(evt));
                break;
            case MetricType.Histogram:
                RecordHistogramMeasurement(evt);
                break;
            default:
                Log.Fatal("Unexpected metric type {MetricType}", MetricType);
                break;
        }
    }

    private void AddToCounter(Event<LogEventData> evt)
    {
        _counter!.Add(MetricValueFromEvent(evt) ?? 1, TagsFromEvent(evt));
    }

    private void AddToUpDownCounter(Event<LogEventData> evt)
    {
        _upDownCounter!.Add(MetricValueFromEvent(evt) ?? 0, TagsFromEvent(evt));
    }

    private void UpdateGauge(long value, KeyValuePair<string, object?>[] tags)
    {
        var tagsKey = string.Join(";", tags.Select(t => $"{t.Key}={t.Value}"));
        _gaugeMeasurements[tagsKey] = new Measurement<long>(value, tags);
    }

    private void RecordHistogramMeasurement(Event<LogEventData> evt)
    {
        if ((MetricValueFromEvent(evt) ?? SpanLengthFromEvent(evt)) is long value)
            _histogram!.Record(value, TagsFromEvent(evt));
    }

    private static long? SpanLengthFromEvent(Event<LogEventData> evt) =>
        SpanStartTimestamp(evt) is string spanStartTimestamp
        && DateTimeOffset.TryParse(spanStartTimestamp, out var spanStart)
            ? (long)(evt.Timestamp - spanStart).TotalMilliseconds
            : null;

    private static object? SpanStartTimestamp(Event<LogEventData> evt)
    {
        return evt.Data.Properties.GetValueOrDefault("@st")
            ?? evt.Data.Properties.GetValueOrDefault("SpanStartTimestamp");
    }

    private long? MetricValueFromEvent(Event<LogEventData> evt)
    {
        if (ValueProperty is string property
            && evt.Data.Properties.GetValueOrDefault(property) is object value)
        {
            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    private KeyValuePair<string, object?>[] TagsFromEvent(Event<LogEventData> evt) =>
        _includedProperties
            .Select(p => new KeyValuePair<string, object?>(p, evt.Data.Properties.GetValueOrDefault(p)))
            .ToArray();

    public void Dispose()
    {
        _meter?.Dispose();
        MeterProvider?.Dispose();
    }
}

public enum MetricType
{
    Counter,
    UpDownCounter,
    Gauge,
    Histogram
}