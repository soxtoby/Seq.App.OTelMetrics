# Seq.App.OTelMetrics

Collect metrics from log events and send them to an OpenTelemetry receiver.

## Getting started

Install the app under _Settings > Apps_. The app package id is `Seq.App.OTelMetrics`.

Configure Seq to _Stream incoming events_ to the app to automatically collect metrics.

Visit the Seq documentation for [detailed information about installing and configuring Seq Apps](https://docs.datalust.co/docs/installing-seq-apps).

## Configuration

Instances of the app support the following properties.

| Property                 | Description                                                                                                                                            |
|--------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Type of metric**       | The type of the metric to be produced. Either Counter, UpDownCounter, Gauge, or Histogram.                                                             |
| **Unit**                 | The unit the metric is tracking (e.g. ms, requests)                                                                                                    |
| **Metric name**          | The name of the metric. If not specified, the title of the app instance will be used.                                                                  |
| **Metric description**   | A description of the metric, which will appear in metric viewers.                                                                                      |
| **Value property**       | The property of the log event to use as the value of the metric.                                                                                       |
| **Included properties**  | Comma-separated list of properties to include as tags.                                                                                                 |
| **OTLP endpoint**        | The endpoint of the OpenTelemetry receiver to send metrics to. If not specified, metrics will be sent to the default OpenTelemetry collector endpoint. |
| **OTLP export protocol** | The OTLP transport protocol to use when sending metrics to the OpenTelemetry receiver. Either Grpc or HttpProtobuf. Defaults to gRPC.                  |

## Types of metrics
### Counter
Tracks a value that increases over time. By default, the value is incremented by 1 for each log event. If a _value property_ is configured, the value of the property will be added to the counter instead.

### UpDownCounter
Tracks a value that may increase or decrease over time, by accumulating the value of the configured _value property_.

### Gauge
Tracks the latest value of the _value property_, for each combination of tags specified by the _included properties_ setting.

### Histogram
Tracks the distribution of measurements. If the _value property_ is not available, the [span duration from traces](https://docs.datalust.co/docs/tracing) will be used.