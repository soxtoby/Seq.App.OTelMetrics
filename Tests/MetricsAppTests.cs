using EasyAssertions;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Core;
using SerilogTracing;
using Xunit.Abstractions;
using MetricType = Seq.App.OTelMetrics.MetricType;

namespace Tests;

public class MetricsAppTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestMetricsApp _sut = new() { CustomMetricName = "test_metric" };

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Counter_NoValueProperty_IncrementsByOne()
    {
        _sut.MetricType = MetricType.Counter;

        using (var logger = CreateLogger())
        {
            logger.Information("test event 1");
            logger.Information("test event 2");
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetSumLong().ShouldBe(2L);
    }

    [Fact]
    public void Counter_WithValueProperty_IncrementsByPropertyValue()
    {
        _sut.MetricType = MetricType.Counter;
        _sut.ValueProperty = "Value";

        using (var logger = CreateLogger())
        {
            logger.Information("test event {Value}", 1);
            logger.Information("test event {Value}", 2);
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetSumLong().ShouldBe(3L);
    }

    [Fact]
    public void UpDownCounter_NoValueProperty_DoesNotIncrement()
    {
        _sut.MetricType = MetricType.UpDownCounter;

        using (var logger = CreateLogger())
        {
            logger.Information("test event 1");
            logger.Information("test event 2");
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetSumLong().ShouldBe(0L);
    }

    [Fact]
    public void UpDownCounter_WithValueProperty_IncrementsByPropertyValue()
    {
        _sut.MetricType = MetricType.UpDownCounter;
        _sut.ValueProperty = "Value";

        using (var logger = CreateLogger())
        {
            logger.Information("test event {Value}", 3);
            logger.Information("test event {Value}", -1);
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetSumLong().ShouldBe(2L);
    }

    [Fact]
    public void Gauge_NoValueProperty_IsZero()
    {
        _sut.MetricType = MetricType.Gauge;

        using (var logger = CreateLogger())
        {
            logger.Information("test event 1");
            logger.Information("test event 2");
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetGaugeLastValueLong().ShouldBe(0L);
    }

    [Fact]
    public void Gauge_WithValueProperty_IsLastValue()
    {
        _sut.MetricType = MetricType.Gauge;
        _sut.ValueProperty = "Value";

        using (var logger = CreateLogger())
        {
            logger.Information("test event {Value}", 1);
            logger.Information("test event {Value}", 2);
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetGaugeLastValueLong().ShouldBe(2L);
    }

    [Fact]
    public void Gauge_WithTags_HasValuePerTagCombination()
    {
        _sut.MetricType = MetricType.Gauge;
        _sut.ValueProperty = "Value";
        _sut.IncludedProperties = "Tag1, Tag2";

        using (var logger = CreateLogger())
        {
            logger.Information("test event {Value} {Tag1} {Tag2}", 1, "1a", "2a");
            logger.Information("test event {Value} {Tag1} {Tag2}", 2, "1a", "2b");
            logger.Information("test event {Value} {Tag1} {Tag2}", 3, "1b", "2a");
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable()
            .ItemsSatisfy( // Note: metric points are newest first
                three =>
                    {
                        three.TagsDictionary().ShouldOnlyContain(new Dictionary<string, object?>
                            {
                                ["Tag1"] = "1b",
                                ["Tag2"] = "2a"
                            });
                        three.GetGaugeLastValueLong().ShouldBe(3L);
                    },
                two =>
                    {
                        two.TagsDictionary().ShouldOnlyContain(new Dictionary<string, object?>
                            {
                                ["Tag1"] = "1a",
                                ["Tag2"] = "2b"
                            });
                        two.GetGaugeLastValueLong().ShouldBe(2L);
                    },
                one =>
                    {
                        one.TagsDictionary().ShouldOnlyContain(new Dictionary<string, object?>
                            {
                                ["Tag1"] = "1a",
                                ["Tag2"] = "2a"
                            });
                        one.GetGaugeLastValueLong().ShouldBe(1L);
                    }
            );
    }

    [Fact]
    public async Task Histogram_PicksUpSpanDuration()
    {
        _sut.MetricType = MetricType.Histogram;
        LoggerActivity activity;

        await using (var logger = CreateLogger())
        {
            activity = logger.StartActivity("test activity");
            await Task.Delay(123);
            activity.Complete();
            _sut.Flush();
        }

        _sut.Metrics
            .ShouldBeASingular<Metric>()
            .And.MetricPointsEnumerable().ShouldBeASingular<MetricPoint>()
            .And.GetHistogramSum().ShouldBe(Math.Floor(activity.Activity!.Duration.TotalMilliseconds));
    }

    Logger CreateLogger() => new LoggerConfiguration()
        .WriteTo.TestOutput(output)
        .AuditTo.SeqApp(_sut)
        .CreateLogger();
}

file static class MetricExtensions
{
    public static IEnumerable<MetricPoint> MetricPointsEnumerable(this Metric metric)
    {
        var enumerator = metric.GetMetricPoints().GetEnumerator();
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    public static Dictionary<string, object?> TagsDictionary(this MetricPoint point)
    {
        var dictionary = new Dictionary<string, object?>();
        var enumerator = point.Tags.GetEnumerator();
        while (enumerator.MoveNext())
            dictionary[enumerator.Current.Key] = enumerator.Current.Value;
        return dictionary;
    }
}