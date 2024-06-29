using Seq.App.OTelMetrics;
using Serilog;

using var log = new LoggerConfiguration()
    .WriteTo.Console()
    .AuditTo.SeqApp<MetricsApp>(new Dictionary<string, string>
        {
            [nameof(MetricsApp.CustomMetricName)] = "test_events",
            [nameof(MetricsApp.MetricType)] = MetricType.Counter.ToString(),
        })
    .CreateLogger();
    
log.Information("Here's one");
log.Information("Here's another");