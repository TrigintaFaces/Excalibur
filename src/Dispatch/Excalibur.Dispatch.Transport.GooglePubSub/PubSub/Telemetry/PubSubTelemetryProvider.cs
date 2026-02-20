// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using GoogleMetric = Google.Api.Metric;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides comprehensive telemetry for Google Pub/Sub operations with OpenTelemetry and Cloud Monitoring integration.
/// </summary>
public sealed class PubSubTelemetryProvider : IDisposable
{
	private readonly ILogger<PubSubTelemetryProvider> _logger;
	private readonly GooglePubSubOptions _options;
	private readonly ActivitySource _activitySource;
	private readonly Meter _meter;

	/// <summary>
	/// Metrics.
	/// </summary>
	private readonly Counter<long> _messagesReceived;

	private readonly Counter<long> _messagesAcknowledged;
	private readonly Counter<long> _messagesNacked;
	private readonly Histogram<double> _messageAge;
	private readonly Histogram<double> _ackLatency;
	private readonly ObservableGauge<int> _activeStreams;
	private readonly ObservableGauge<double> _throughput;
#if NET9_0_OR_GREATER

	private readonly Lock _throughputLock = new();

#else
	private readonly object _throughputLock = new();

#endif
	private MeterProvider? _meterProvider;
	private TracerProvider? _tracerProvider;

	/// <summary>
	/// State tracking.
	/// </summary>
	private long _lastMessageCount;

	private DateTimeOffset _lastThroughputCheck;
	private int _currentActiveStreams;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTelemetryProvider" /> class.
	/// </summary>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="options"> Pub/Sub options. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public PubSubTelemetryProvider(
		ILogger<PubSubTelemetryProvider> logger,
		IOptions<GooglePubSubOptions> options)
		: this(logger, options, meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTelemetryProvider" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="options"> Pub/Sub options. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public PubSubTelemetryProvider(
		ILogger<PubSubTelemetryProvider> logger,
		IOptions<GooglePubSubOptions> options,
		IMeterFactory? meterFactory)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));

		// Create activity source for distributed tracing
		_activitySource = new ActivitySource(
			GooglePubSubTelemetryConstants.ActivitySourceName,
			GooglePubSubTelemetryConstants.Version);

		// Create meter for metrics
		_meter = meterFactory?.Create(GooglePubSubTelemetryConstants.MeterName)
			?? new Meter(
				GooglePubSubTelemetryConstants.MeterName,
				GooglePubSubTelemetryConstants.Version);

		// Initialize metrics
		_messagesReceived = _meter.CreateCounter<long>(
			"dispatch.pubsub.messages",
			"messages",
			"Total number of messages received");

		_messagesAcknowledged = _meter.CreateCounter<long>(
			"dispatch.pubsub.messages.acknowledged",
			"messages",
			"Total number of messages acknowledged");

		_messagesNacked = _meter.CreateCounter<long>(
			"dispatch.pubsub.messages.nacked",
			"messages",
			"Total number of messages nacked");

		_messageAge = _meter.CreateHistogram<double>(
			"dispatch.pubsub.message_age",
			"seconds",
			"Age of messages when received");

		_ackLatency = _meter.CreateHistogram<double>(
			"dispatch.pubsub.ack_latency",
			"milliseconds",
			"Time taken to acknowledge messages");

		_activeStreams = _meter.CreateObservableGauge(
			"dispatch.pubsub.active_streams",
			() => _currentActiveStreams,
			"streams",
			"Number of active streaming pull connections");

		_throughput = _meter.CreateObservableGauge(
			"dispatch.pubsub.throughput",
			ObserveThroughput,
			"messages/second",
			"Current message processing throughput");

		// Initialize OpenTelemetry if enabled
		if (_options.EnableOpenTelemetry)
		{
			InitializeOpenTelemetry();
		}

		_lastThroughputCheck = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Records a message being received.
	/// </summary>
	/// <param name="message"> The received message. </param>
	/// <param name="subscription"> The subscription name. </param>
	/// <returns> An activity for tracing the message processing. </returns>
	public Activity? RecordMessageReceived(PubsubMessage message, string subscription)
	{
		// Start activity for distributed tracing
		var activity = _activitySource.StartActivity(
			"pubsub.receive",
			ActivityKind.Consumer,
			ExtractParentContext(message));

		_ = activity?.SetTag(GooglePubSubTelemetry.Tags.MessageId, message.MessageId);
		_ = activity?.SetTag(GooglePubSubTelemetry.Tags.Subscription, subscription);

		if (!string.IsNullOrEmpty(message.OrderingKey))
		{
			_ = activity?.SetTag(GooglePubSubTelemetry.Tags.OrderingKey, message.OrderingKey);
		}

		// Record metrics
		var tags = new TagList { { GooglePubSubTelemetry.Tags.Subscription, subscription } };

		_messagesReceived.Add(1, tags);

		// Calculate and record message age
		if (message.PublishTime != null)
		{
			var age = (DateTimeOffset.UtcNow - message.PublishTime.ToDateTime()).TotalSeconds;
			_messageAge.Record(age, tags);
			_ = activity?.SetTag("pubsub.message_age_seconds", age);
		}

		_ = Interlocked.Increment(ref _lastMessageCount);

		return activity;
	}

	/// <summary>
	/// Records a message being acknowledged.
	/// </summary>
	/// <param name="messageId"> The message ID (reserved for future correlation tracking). </param>
	/// <param name="subscription"> The subscription name. </param>
	/// <param name="processingTime"> Time taken to process the message. </param>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "messageId reserved for future correlation and tracing enhancements")]
	public void RecordMessageAcknowledged(string messageId, string subscription, TimeSpan processingTime)
	{
		var tags = new TagList { { GooglePubSubTelemetry.Tags.Subscription, subscription } };

		_messagesAcknowledged.Add(1, tags);
		_ackLatency.Record(processingTime.TotalMilliseconds, tags);
	}

	/// <summary>
	/// Records a message being nacked.
	/// </summary>
	/// <param name="messageId"> The message ID (reserved for future correlation tracking). </param>
	/// <param name="subscription"> The subscription name. </param>
	/// <param name="reason"> The reason for nacking. </param>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "messageId reserved for future correlation and tracing enhancements")]
	public void RecordMessageNacked(string messageId, string subscription, string reason)
	{
		var tags = new TagList
		{
			{ GooglePubSubTelemetry.Tags.Subscription, subscription }, { GooglePubSubTelemetry.Tags.ErrorType, reason },
		};

		_messagesNacked.Add(1, tags);
	}

	/// <summary>
	/// Updates the number of active streams.
	/// </summary>
	/// <param name="count"> The current number of active streams. </param>
	public void UpdateActiveStreams(int count) => _currentActiveStreams = count;

	/// <summary>
	/// Exports custom metrics to Google Cloud Monitoring.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the export operation. </returns>
	public async Task ExportToCloudMonitoringAsync(CancellationToken cancellationToken)
	{
		if (!_options.ExportToCloudMonitoring)
		{
			return;
		}

		try
		{
			var client = await MetricServiceClient.CreateAsync(cancellationToken).ConfigureAwait(false);
			var projectName = global::Google.Api.Gax.ResourceNames.ProjectName.FromProject(_options.ProjectId);

			// Create time series for custom metrics
			var timeSeries = new List<TimeSeries>();
			var now = DateTimeOffset.UtcNow;
			var interval = new TimeInterval { EndTime = Timestamp.FromDateTimeOffset(now) };

			// Add throughput metric
			var throughput = CalculateThroughput();
			if (throughput > 0)
			{
				timeSeries.Add(CreateTimeSeries(
					"custom.googleapis.com/pubsub/throughput",
					throughput,
					interval,
					_options.SubscriptionName));
			}

			// Add active streams metric
			timeSeries.Add(CreateTimeSeries(
				"custom.googleapis.com/pubsub/active_streams",
				_currentActiveStreams,
				interval,
				_options.SubscriptionName));

			if (timeSeries.Count > 0)
			{
				await client.CreateTimeSeriesAsync(projectName, timeSeries, cancellationToken)
					.ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to export metrics to Cloud Monitoring");
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_activitySource?.Dispose();
		_meter?.Dispose();
		_meterProvider?.Dispose();
		_tracerProvider?.Dispose();
	}

	private static TimeSeries CreateTimeSeries(
		string metricType,
		double value,
		TimeInterval interval,
		string subscription)
	{
		var metric = new GoogleMetric { Type = metricType };
		metric.Labels["subscription"] = subscription;

		var resource = new MonitoredResource { Type = "pubsub_subscription" };
		resource.Labels["subscription_id"] = subscription;

		return new TimeSeries
		{
			Metric = metric,
			Resource = resource,
			Points = { new Point { Interval = interval, Value = new TypedValue { DoubleValue = value } } },
		};
	}

	private void InitializeOpenTelemetry()
	{
		var resourceBuilder = ResourceBuilder.CreateDefault()
			.AddService(
				serviceName: "excalibur-dispatch-pubsub",
				serviceVersion: GooglePubSubTelemetryConstants.Version)
			.AddAttributes(new Dictionary<string, object>
				(StringComparer.Ordinal)
			{
				["cloud.provider"] = "gcp",
				["cloud.platform"] = "gcp_pubsub",
				["cloud.account.id"] = _options.ProjectId,
			});

		// Configure metrics
		_meterProvider = Sdk.CreateMeterProviderBuilder()
			.SetResourceBuilder(resourceBuilder)
			.AddMeter(GooglePubSubTelemetryConstants.ActivitySourceName)
			.AddOtlpExporter(options => options.Endpoint = new Uri(_options.OtlpEndpoint ?? "http://localhost:4317"))
			.Build();

		// Configure tracing
		_tracerProvider = Sdk.CreateTracerProviderBuilder()
			.SetResourceBuilder(resourceBuilder)
			.AddSource(GooglePubSubTelemetryConstants.ActivitySourceName)
			.AddOtlpExporter(options => options.Endpoint = new Uri(_options.OtlpEndpoint ?? "http://localhost:4317"))
			.Build();

		_logger.LogInformation(
			"OpenTelemetry initialized for Google Pub/Sub with endpoint {Endpoint}",
			_options.OtlpEndpoint);
	}

	private static ActivityContext ExtractParentContext(PubsubMessage message)
	{
		// Extract trace context from message attributes if present
		if (message.Attributes.TryGetValue("traceparent", out var traceparent) &&
			ActivityContext.TryParse(traceparent, traceState: null, out var context))
		{
			return context;
		}

		return default;
	}

	private double ObserveThroughput()
	{
		lock (_throughputLock)
		{
			return CalculateThroughput();
		}
	}

	private double CalculateThroughput()
	{
		var now = DateTimeOffset.UtcNow;
		var elapsed = (now - _lastThroughputCheck).TotalSeconds;

		if (elapsed <= 0)
		{
			return 0;
		}

		var currentCount = Interlocked.Read(ref _lastMessageCount);
		var throughput = currentCount / elapsed;

		_lastThroughputCheck = now;
		_ = Interlocked.Exchange(ref _lastMessageCount, 0);

		return throughput;
	}
}
