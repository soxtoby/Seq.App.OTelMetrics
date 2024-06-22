using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Metrics;

[SeqApp("Metrics", Description = "Uses a provided template to send events as formatted email.")]
public class MetricsApp : SeqApp, ISubscribeTo<LogEventData>, IDisposable
{
    private Counter<long>? _counter;
    private UpDownCounter<long>? _upDownCounter;
    private readonly ConcurrentDictionary<string, Measurement<long>> _gaugeMeasurements = new();
    private Histogram<long>? _histogram;
    private string[] _includedProperties = [];

    [SeqAppSetting(
        DisplayName = "Metric name",
        HelpText = "The name of the metric to expose. It's recommended you follow the naming conventions here: https://github.com/open-telemetry/semantic-conventions/blob/main/docs/general/metrics.md"
    )]
    public string MetricName { get; set; } = "seq_app_metric";

    [SeqAppSetting(
        DisplayName = "Metric description",
        IsOptional = true
    )]
    public string? Description { get; set; }

    [SeqAppSetting(DisplayName = "Type of metric")]
    public MetricType MetricType { get; set; }

    [SeqAppSetting(
        DisplayName = "Unit",
        HelpText = "Unit of measurement the metric represents.",
        IsOptional = true
    )]
    public string? Unit { get; set; }

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

    protected override void OnAttached()
    {
        switch (MetricType)
        {
            case MetricType.Counter:
                _counter = Metrics.CreateCounter(MetricName, Unit, Description);
                break;
            case MetricType.UpDownCounter:
                _upDownCounter = Metrics.CreateUpDownCounter(MetricName, Unit, Description);
                break;
            case MetricType.Gauge:
                Metrics.CreateGauge(MetricName, () => _gaugeMeasurements.Values, Unit, Description);
                break;
            case MetricType.Histogram:
                _histogram = Metrics.CreateHistogram(MetricName, Unit, Description);
                break;
            default:
                Log.Fatal("Unexpected metric type {MetricType}", MetricType);
                break;
        }

        _includedProperties = IncludedProperties?.Split(',').Select(p => p.Trim()).ToArray() ?? [];
    }

    public void On(Event<LogEventData> evt)
    {
        switch (MetricType)
        {
            case MetricType.Counter:
                _counter!.Add(MetricValueFromEvent(evt) ?? 1, TagsFromEvent(evt));
                break;
            case MetricType.UpDownCounter:
                _upDownCounter!.Add(MetricValueFromEvent(evt) ?? 0, TagsFromEvent(evt));
                break;
            case MetricType.Gauge:
                UpdateGauge(MetricValueFromEvent(evt) ?? 0, TagsFromEvent(evt));
                break;
            case MetricType.Histogram:
                _histogram!.Record(MetricValueFromEvent(evt) ?? SpanLengthFromEvent(evt), TagsFromEvent(evt));
                break;
            default:
                Log.Fatal("Unexpected metric type {MetricType}", MetricType);
                break;
        }
    }

    private void UpdateGauge(long value, KeyValuePair<string, object?>[] tags)
    {
        var tagsKey = string.Join(";", tags.Select(t => $"{t.Key}={t.Value}"));
        _gaugeMeasurements[tagsKey] = new Measurement<long>(value, tags);
    }

    private static long SpanLengthFromEvent(Event<LogEventData> evt) =>
        evt.Data.Properties.GetValueOrDefault("@st") is DateTimeOffset spanStart
            ? (long)(spanStart - evt.Timestamp).TotalMilliseconds
            : 0;

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

    public void Dispose() => Metrics.AppDisposed();
}

public enum MetricType
{
    Counter,
    UpDownCounter,
    Gauge,
    Histogram
}