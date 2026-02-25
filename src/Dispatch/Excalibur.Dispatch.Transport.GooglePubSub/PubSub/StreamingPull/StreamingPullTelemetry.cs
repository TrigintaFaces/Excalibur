// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides telemetry instrumentation for Google Pub/Sub streaming pull operations.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="ActivitySource"/> for distributed tracing and <see cref="Meter"/> for metrics,
/// following OpenTelemetry semantic conventions for messaging systems.
/// </para>
/// <para>
/// All tag dimensions that accept user-controlled values (stream IDs, subscription names) are
/// protected by <see cref="TagCardinalityGuard"/> to prevent unbounded metric cardinality.
/// </para>
/// </remarks>
public sealed class StreamingPullTelemetry : IDisposable
{
	/// <summary>
	/// The meter name for streaming pull metrics.
	/// </summary>
	/// <remarks>
	/// Delegates to <see cref="GooglePubSubTelemetryConstants.MeterName"/> for consistency
	/// across all Google Pub/Sub components.
	/// </remarks>
	public const string MeterName = GooglePubSubTelemetryConstants.MeterName;

	/// <summary>
	/// The activity source name for streaming pull operations.
	/// </summary>
	/// <remarks>
	/// Delegates to <see cref="GooglePubSubTelemetryConstants.ActivitySourceName"/> for consistency
	/// across all Google Pub/Sub components.
	/// </remarks>
	public const string ActivitySourceName = GooglePubSubTelemetryConstants.ActivitySourceName;

	private const string Version = GooglePubSubTelemetryConstants.Version;

	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TagCardinalityGuard _streamIdGuard;
	private readonly TagCardinalityGuard _subscriptionGuard;

	private readonly Counter<long> _messagesReceived;
	private readonly Counter<long> _acksSent;
	private readonly Counter<long> _nacksSent;
	private readonly Counter<long> _streamReconnections;
	private readonly Counter<long> _streamsOpened;
	private readonly Counter<long> _streamsClosed;
	private readonly Histogram<double> _processingDuration;
	private readonly Histogram<double> _ackLatency;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPullTelemetry" /> class.
	/// </summary>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by this class and disposed in Dispose()")]
	public StreamingPullTelemetry()
		: this(meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPullTelemetry" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or this class and disposed in Dispose()")]
	public StreamingPullTelemetry(IMeterFactory? meterFactory)
	{
		_meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName, Version);
		_activitySource = new ActivitySource(ActivitySourceName, Version);
		_streamIdGuard = new TagCardinalityGuard(maxCardinality: 100);
		_subscriptionGuard = new TagCardinalityGuard(maxCardinality: 100);

		_messagesReceived = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.messages.received", "messages", "Total messages received via streaming pull");

		_acksSent = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.acks.sent", "acks", "Total acknowledgments sent");

		_nacksSent = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.nacks.sent", "nacks", "Total negative acknowledgments sent");

		_streamReconnections = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.streams.reconnections", "reconnections", "Total stream reconnection attempts");

		_streamsOpened = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.streams.opened", "streams", "Total streams opened");

		_streamsClosed = _meter.CreateCounter<long>(
			"dispatch.streaming_pull.streams.closed", "streams", "Total streams closed");

		_processingDuration = _meter.CreateHistogram<double>(
			"dispatch.streaming_pull.message.processing_duration", "ms", "Message processing duration in milliseconds");

		_ackLatency = _meter.CreateHistogram<double>(
			"dispatch.streaming_pull.ack.latency", "ms", "Acknowledgment latency in milliseconds");
	}

	/// <summary>
	/// Starts an activity for a streaming pull operation.
	/// </summary>
	/// <param name="operationName"> The name of the operation. </param>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="subscription"> The subscription name. </param>
	/// <returns> The started activity, or null if no listeners are registered. </returns>
	public Activity? StartStreamActivity(string operationName, string streamId, string? subscription = null)
	{
		var activity = _activitySource.StartActivity(operationName, ActivityKind.Consumer);
		if (activity != null)
		{
			activity.SetTag("messaging.system", "gcp_pubsub");
			activity.SetTag("messaging.operation", operationName);
			activity.SetTag("dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId));

			if (subscription != null)
			{
				activity.SetTag("messaging.destination", _subscriptionGuard.Guard(subscription));
			}
		}

		return activity;
	}

	/// <summary>
	/// Records that a message was received from a streaming pull connection.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="subscription"> The subscription name. </param>
	public void RecordMessageReceived(string streamId, string? subscription = null)
	{
		var tags = new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		};

		if (subscription != null)
		{
			tags.Add("messaging.destination", _subscriptionGuard.Guard(subscription));
		}

		_messagesReceived.Add(1, tags);
	}

	/// <summary>
	/// Records that an acknowledgment was sent.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="latencyMs"> The acknowledgment latency in milliseconds. </param>
	public void RecordAck(string streamId, double latencyMs)
	{
		var tags = new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		};

		_acksSent.Add(1, tags);
		_ackLatency.Record(latencyMs, tags);
	}

	/// <summary>
	/// Records that a negative acknowledgment was sent.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RecordNack(string streamId)
	{
		_nacksSent.Add(1, new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		});
	}

	/// <summary>
	/// Records a stream reconnection attempt.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RecordReconnection(string streamId)
	{
		_streamReconnections.Add(1, new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		});
	}

	/// <summary>
	/// Records that a stream was opened.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="subscription"> The subscription name. </param>
	public void RecordStreamOpened(string streamId, string? subscription = null)
	{
		var tags = new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		};

		if (subscription != null)
		{
			tags.Add("messaging.destination", _subscriptionGuard.Guard(subscription));
		}

		_streamsOpened.Add(1, tags);
	}

	/// <summary>
	/// Records that a stream was closed.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	public void RecordStreamClosed(string streamId)
	{
		_streamsClosed.Add(1, new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		});
	}

	/// <summary>
	/// Records the duration of message processing.
	/// </summary>
	/// <param name="streamId"> The stream identifier. </param>
	/// <param name="durationMs"> The processing duration in milliseconds. </param>
	public void RecordProcessingDuration(string streamId, double durationMs)
	{
		_processingDuration.Record(durationMs, new TagList
		{
			{ "dispatch.streaming_pull.stream_id", _streamIdGuard.Guard(streamId) },
		});
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_activitySource.Dispose();
		_meter.Dispose();
	}
}
