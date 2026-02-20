// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Comprehensive telemetry provider for Google Cloud Pub/Sub operations. Handles metrics, distributed tracing, and Cloud Monitoring integration.
/// </summary>
public sealed class PubSubTelemetryEnhanced : IDisposable
{
	/// <summary>
	/// Trace context propagation.
	/// </summary>
	private const string TraceParentHeader = "googclient-trace-parent";

	private const string TraceStateHeader = "googclient-trace-state";
	private readonly GooglePubSubOptions _options;
	private readonly ILogger<PubSubTelemetryEnhanced> _logger;
	private readonly ActivitySource _activitySource;
	private readonly Meter _meter;

	/// <summary>
	/// Counters.
	/// </summary>
	private readonly Counter<long> _messagesPublished;

	private readonly Counter<long> _messagesReceived;
	private readonly Counter<long> _messagesAcknowledged;
	private readonly Counter<long> _messagesNacked;
	private readonly Counter<long> _messagesDeadLettered;
	private readonly Counter<long> _messagesRetried;
	private readonly Counter<long> _acknowledgmentsFailed;
	private readonly Counter<long> _publishFailures;

	/// <summary>
	/// Histograms.
	/// </summary>
	private readonly Histogram<double> _publishLatency;

	private readonly Histogram<double> _receiveLatency;
	private readonly Histogram<double> _acknowledgmentLatency;
	private readonly Histogram<double> _endToEndLatency;
	private readonly Histogram<double> _messageSize;
	private readonly Histogram<long> _batchSize;
	private readonly Histogram<long> _retryCount;

	/// <summary>
	/// Gauges.
	/// </summary>
	private readonly ObservableGauge<int> _activeStreams;

	private readonly ObservableGauge<int> _pendingAcknowledgments;
	private readonly ObservableGauge<int> _flowControlPermits;
	private readonly ObservableGauge<long> _flowControlBytes;

	/// <summary>
	/// Internal state for observable gauges.
	/// </summary>
	private int _currentActiveStreams;

	private int _currentPendingAcks;
	private int _currentFlowControlPermits;
	private long _currentFlowControlBytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTelemetryEnhanced" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public PubSubTelemetryEnhanced(
		IOptions<GooglePubSubOptions> options,
		ILogger<PubSubTelemetryEnhanced> logger)
		: this(options, logger, meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubTelemetryEnhanced" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="options"> Pub/Sub options. </param>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public PubSubTelemetryEnhanced(
		IOptions<GooglePubSubOptions> options,
		ILogger<PubSubTelemetryEnhanced> logger,
		IMeterFactory? meterFactory)
	{
		_options = options.Value;
		_logger = logger;

		// Initialize Activity Source for distributed tracing
		_activitySource = new ActivitySource(
			GooglePubSubTelemetryConstants.ActivitySourceName,
			GooglePubSubTelemetryConstants.Version);

		// Initialize Meter for metrics
		_meter = meterFactory?.Create(GooglePubSubTelemetryConstants.MeterName)
			?? new Meter(
				GooglePubSubTelemetryConstants.MeterName,
				GooglePubSubTelemetryConstants.Version);

		// Initialize counters
		_messagesPublished = _meter.CreateCounter<long>(
			"pubsub.messages.published",
			"messages",
			"Total messages published to Pub/Sub");

		_messagesReceived = _meter.CreateCounter<long>(
			"pubsub.messages.received",
			"messages",
			"Total messages received from Pub/Sub");

		_messagesAcknowledged = _meter.CreateCounter<long>(
			"pubsub.messages.acknowledged",
			"messages",
			"Total messages successfully acknowledged");

		_messagesNacked = _meter.CreateCounter<long>(
			"pubsub.messages.nacked",
			"messages",
			"Total messages negatively acknowledged");

		_messagesDeadLettered = _meter.CreateCounter<long>(
			"pubsub.messages.dead_lettered",
			"messages",
			"Total messages sent to dead letter topic");

		_messagesRetried = _meter.CreateCounter<long>(
			"pubsub.messages.retried",
			"messages",
			"Total message retry attempts");

		_acknowledgmentsFailed = _meter.CreateCounter<long>(
			"pubsub.acknowledgments.failed",
			"acknowledgments",
			"Total failed acknowledgment operations");

		_publishFailures = _meter.CreateCounter<long>(
			"pubsub.publish.failures",
			"operations",
			"Total failed publish operations");

		// Initialize histograms
		_publishLatency = _meter.CreateHistogram<double>(
			"pubsub.publish.latency",
			"milliseconds",
			"Latency of publish operations");

		_receiveLatency = _meter.CreateHistogram<double>(
			"pubsub.receive.latency",
			"milliseconds",
			"Latency of receive operations");

		_acknowledgmentLatency = _meter.CreateHistogram<double>(
			"pubsub.acknowledgment.latency",
			"milliseconds",
			"Latency of acknowledgment operations");

		_endToEndLatency = _meter.CreateHistogram<double>(
			"pubsub.message.end_to_end_latency",
			"milliseconds",
			"End-to-end message processing latency");

		_messageSize = _meter.CreateHistogram<double>(
			"pubsub.message.size",
			"bytes",
			"Size of messages in bytes");

		_batchSize = _meter.CreateHistogram<long>(
			"pubsub.batch.size",
			"messages",
			"Number of messages in batches");

		_retryCount = _meter.CreateHistogram<long>(
			"pubsub.message.retry_count",
			"retries",
			"Number of retries per message");

		// Initialize observable gauges
		_activeStreams = _meter.CreateObservableGauge(
			"pubsub.streams.active",
			() => _currentActiveStreams,
			"streams",
			"Number of active streaming pull connections");

		_pendingAcknowledgments = _meter.CreateObservableGauge(
			"pubsub.acknowledgments.pending",
			() => _currentPendingAcks,
			"acknowledgments",
			"Number of pending acknowledgments");

		_flowControlPermits = _meter.CreateObservableGauge(
			"pubsub.flow_control.permits",
			() => _currentFlowControlPermits,
			"permits",
			"Available flow control permits");

		_flowControlBytes = _meter.CreateObservableGauge(
			"pubsub.flow_control.bytes",
			() => _currentFlowControlBytes,
			"bytes",
			"Available flow control bytes");
	}

	/// <summary>
	/// Starts an activity for message publishing.
	/// </summary>
	public Activity? StartPublishActivity(PubsubMessage message, string topic)
	{
		if (!_options.EnableOpenTelemetry)
		{
			return null;
		}

		var activity = _activitySource.StartActivity(
			"PubSub.Publish",
			ActivityKind.Producer);

		if (activity != null)
		{
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.Topic, topic);
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.ProjectId, _options.ProjectId);
			_ = activity.SetTag("messaging.system", "pubsub");
			_ = activity.SetTag("messaging.message.payload_size_bytes", message.Data.Length);

			if (message.OrderingKey != null)
			{
				_ = activity.SetTag(GooglePubSubTelemetry.Tags.OrderingKey, message.OrderingKey);
			}

			// Inject trace context into message attributes
			if (_options.EnableTracePropagation && activity.Context != default)
			{
				message.Attributes[TraceParentHeader] = activity.Context.TraceId + "-" + activity.Context.SpanId;
				if (!string.IsNullOrEmpty(activity.Context.TraceState))
				{
					message.Attributes[TraceStateHeader] = activity.Context.TraceState;
				}
			}
		}

		return activity;
	}

	/// <summary>
	/// Records successful message publish.
	/// </summary>
	public void RecordPublish(Activity? activity, string messageId, TimeSpan duration)
	{
		_messagesPublished.Add(
			1,
			new KeyValuePair<string, object?>("topic", _options.TopicId),
			new KeyValuePair<string, object?>("project", _options.ProjectId));

		_publishLatency.Record(
			duration.TotalMilliseconds,
			new KeyValuePair<string, object?>("topic", _options.TopicId));

		if (activity != null)
		{
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.MessageId, messageId);
			_ = activity.SetStatus(ActivityStatusCode.Ok);
		}
	}

	/// <summary>
	/// Records failed message publish.
	/// </summary>
	public void RecordPublishFailure(Activity? activity, Exception exception)
	{
		_publishFailures.Add(
			1,
			new KeyValuePair<string, object?>("topic", _options.TopicId),
			new KeyValuePair<string, object?>("error_type", exception.GetType().Name));

		if (activity != null)
		{
			_ = activity.SetStatus(ActivityStatusCode.Error, exception.Message);

			// Record exception as event instead of using RecordException extension
			_ = activity.AddEvent(new ActivityEvent(
				"exception",
				DateTimeOffset.UtcNow,
				new ActivityTagsCollection
				{
					{ "exception.type", exception.GetType().FullName },
					{ "exception.message", exception.Message },
					{ "exception.stacktrace", exception.StackTrace },
				}));
		}
	}

	/// <summary>
	/// Starts an activity for message receiving.
	/// </summary>
	public Activity? StartReceiveActivity(PubsubMessage message)
	{
		if (!_options.EnableOpenTelemetry)
		{
			return null;
		}

		Activity? activity = null;

		// Extract parent context if trace propagation is enabled
		if (_options.EnableTracePropagation &&
			message.Attributes.TryGetValue(TraceParentHeader, out var traceParent))
		{
			// Parse trace parent
			if (ActivityContext.TryParse(traceParent, traceState: null, out var parentContext))
			{
				activity = _activitySource.StartActivity(
					"PubSub.Receive",
					ActivityKind.Consumer,
					parentContext);
			}
		}

		// Start new activity if no parent context
		activity ??= _activitySource.StartActivity(
			"PubSub.Receive",
			ActivityKind.Consumer);

		if (activity != null)
		{
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.MessageId, message.MessageId);
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.Subscription, _options.SubscriptionId);
			_ = activity.SetTag(GooglePubSubTelemetry.Tags.ProjectId, _options.ProjectId);
			_ = activity.SetTag("messaging.system", "pubsub");
			_ = activity.SetTag("messaging.message.payload_size_bytes", message.Data.Length);

			if (message.OrderingKey != null)
			{
				_ = activity.SetTag(GooglePubSubTelemetry.Tags.OrderingKey, message.OrderingKey);
			}

			// Calculate receive latency if publish time is available
			if (message.PublishTime != null)
			{
				var publishTime = message.PublishTime.ToDateTime();
				var receiveLatency = DateTimeOffset.UtcNow - publishTime;
				_ = activity.SetTag("messaging.pubsub.receive_latency_ms", receiveLatency.TotalMilliseconds);
			}
		}

		return activity;
	}

	/// <summary>
	/// Records message receipt.
	/// </summary>
	public void RecordReceive(PubsubMessage message, Activity? activity = null)
	{
		_messagesReceived.Add(
			1,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId),
			new KeyValuePair<string, object?>("project", _options.ProjectId));

		_messageSize.Record(
			message.Data.Length,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));

		// Record receive latency if publish time is available
		if (message.PublishTime != null)
		{
			var publishTime = message.PublishTime.ToDateTime();
			var receiveLatency = (DateTimeOffset.UtcNow - publishTime).TotalMilliseconds;
			_receiveLatency.Record(
				receiveLatency,
				new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));
		}

		_ = activity?.AddEvent(new ActivityEvent("Message received"));
	}

	/// <summary>
	/// Records message acknowledgment.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "ackId reserved for future per-acknowledgment correlation tracking")]
	public void RecordAcknowledgment(string ackId, TimeSpan duration, bool success)
	{
		if (success)
		{
			_messagesAcknowledged.Add(
				1,
				new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));

			_acknowledgmentLatency.Record(
				duration.TotalMilliseconds,
				new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));
		}
		else
		{
			_acknowledgmentsFailed.Add(
				1,
				new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));
		}
	}

	/// <summary>
	/// Records message negative acknowledgment.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "messageId reserved for future per-message nack correlation")]
	public void RecordNack(string messageId, string reason) =>
		_messagesNacked.Add(
			1,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId),
			new KeyValuePair<string, object?>("reason", reason));

	/// <summary>
	/// Records message sent to dead letter.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "messageId reserved for future dead letter correlation tracking")]
	public void RecordDeadLetter(string messageId, int deliveryAttempt) =>
		_messagesDeadLettered.Add(
			1,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId),
			new KeyValuePair<string, object?>("delivery_attempt", deliveryAttempt));

	/// <summary>
	/// Records message retry.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "messageId reserved for future per-message retry correlation")]
	public void RecordRetry(string messageId, int retryCount)
	{
		_messagesRetried.Add(
			1,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));

		_retryCount.Record(
			retryCount,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));
	}

	/// <summary>
	/// Records batch processing.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "duration reserved for future batch processing latency tracking")]
	public void RecordBatch(int size, TimeSpan duration) =>
		_batchSize.Record(
			size,
			new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));

	/// <summary>
	/// Updates active stream count.
	/// </summary>
	public void UpdateActiveStreams(int count) => _currentActiveStreams = count;

	/// <summary>
	/// Updates pending acknowledgment count.
	/// </summary>
	public void UpdatePendingAcknowledgments(int count) => _currentPendingAcks = count;

	/// <summary>
	/// Updates flow control state.
	/// </summary>
	public void UpdateFlowControl(int permits, long bytes)
	{
		_currentFlowControlPermits = permits;
		_currentFlowControlBytes = bytes;
	}

	/// <summary>
	/// Records end-to-end message processing.
	/// </summary>
	public void RecordEndToEndLatency(PubsubMessage message, Activity? activity)
	{
		if (message.PublishTime != null)
		{
			var publishTime = message.PublishTime.ToDateTime();
			var endToEndLatency = (DateTimeOffset.UtcNow - publishTime).TotalMilliseconds;

			_endToEndLatency.Record(
				endToEndLatency,
				new KeyValuePair<string, object?>("subscription", _options.SubscriptionId));

			_ = activity?.SetTag("messaging.pubsub.end_to_end_latency_ms", endToEndLatency);
		}
	}

	/// <summary>
	/// Creates a linked activity for cross-service correlation.
	/// </summary>
	public Activity? CreateLinkedActivity(string operationName, ActivityContext parentContext)
	{
		if (!_options.EnableOpenTelemetry)
		{
			return null;
		}

		var links = new[] { new ActivityLink(parentContext) };

		return _activitySource.StartActivity(
			operationName,
			ActivityKind.Internal,
			parentContext: default,
			links: links);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_activitySource?.Dispose();
		_meter?.Dispose();
	}
}
