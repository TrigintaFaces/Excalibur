// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides OpenTelemetry-compatible metrics for transport adapter operations using System.Diagnostics.Metrics.
/// </summary>
/// <remarks>
/// <para>
/// This follows .NET best practices by using built-in APIs that OpenTelemetry can collect from,
/// without requiring a direct dependency on OpenTelemetry.
/// </para>
/// <para>
/// Metric names follow the OpenTelemetry semantic conventions with the <c>dispatch.transport</c> prefix:
/// <list type="bullet">
///   <item><c>dispatch.transport.messages_sent_total</c> - Counter of messages sent</item>
///   <item><c>dispatch.transport.messages_received_total</c> - Counter of messages received</item>
///   <item><c>dispatch.transport.errors_total</c> - Counter of transport errors</item>
///   <item><c>dispatch.transport.send_duration_ms</c> - Histogram of send operation durations</item>
///   <item><c>dispatch.transport.receive_duration_ms</c> - Histogram of receive operation durations</item>
///   <item><c>dispatch.transport.connection_status</c> - Gauge of connection status (0=disconnected, 1=connected)</item>
/// </list>
/// </para>
/// </remarks>
public static class TransportMeter
{
	/// <summary>
	/// The meter name for transport metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Transport";

	/// <summary>
	/// The meter version.
	/// </summary>
	public const string MeterVersion = "1.0.0";

	private static readonly Meter Meter = new(MeterName, MeterVersion);

	/// <summary>
	/// Counter for messages sent.
	/// </summary>
	private static readonly Counter<long> MessagesSentCounter = Meter.CreateCounter<long>(
		"dispatch.transport.messages_sent_total",
		unit: "messages",
		description: "Total number of messages sent through transports");

	/// <summary>
	/// Counter for messages received.
	/// </summary>
	private static readonly Counter<long> MessagesReceivedCounter = Meter.CreateCounter<long>(
		"dispatch.transport.messages_received_total",
		unit: "messages",
		description: "Total number of messages received from transports");

	/// <summary>
	/// Counter for transport errors.
	/// </summary>
	private static readonly Counter<long> ErrorsCounter = Meter.CreateCounter<long>(
		"dispatch.transport.errors_total",
		unit: "errors",
		description: "Total number of transport errors");

	/// <summary>
	/// Histogram for send operation duration.
	/// </summary>
	private static readonly Histogram<double> SendDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.transport.send_duration_ms",
		unit: "milliseconds",
		description: "Duration of send operations");

	/// <summary>
	/// Histogram for receive operation duration.
	/// </summary>
	private static readonly Histogram<double> ReceiveDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.transport.receive_duration_ms",
		unit: "milliseconds",
		description: "Duration of receive operations");

	/// <summary>
	/// Counter for transport starts.
	/// </summary>
	private static readonly Counter<long> TransportStartsCounter = Meter.CreateCounter<long>(
		"dispatch.transport.starts_total",
		unit: "starts",
		description: "Total number of transport starts");

	/// <summary>
	/// Counter for transport stops.
	/// </summary>
	private static readonly Counter<long> TransportStopsCounter = Meter.CreateCounter<long>(
		"dispatch.transport.stops_total",
		unit: "stops",
		description: "Total number of transport stops");

	/// <summary>
	/// Observable gauges for transport state.
	/// </summary>
	private static readonly Dictionary<string, TransportMetricsState> TransportStates = [];

#if NET9_0_OR_GREATER
	private static readonly Lock StateLock = new();
#else
	private static readonly object StateLock = new();
#endif

	static TransportMeter()
	{
		// Create observable gauge for connection status
		_ = Meter.CreateObservableGauge(
			"dispatch.transport.connection_status",
			observeValues: static () =>
			{
				lock (StateLock)
				{
					return TransportStates.Select(static kvp => new Measurement<int>(
						kvp.Value.IsConnected ? 1 : 0,
						new KeyValuePair<string, object?>("transport_name", kvp.Key),
						new KeyValuePair<string, object?>("transport_type", kvp.Value.TransportType)));
				}
			},
			unit: "status",
			description: "Transport connection status (0=disconnected, 1=connected)");

		// Create observable gauge for pending messages (queue depth)
		_ = Meter.CreateObservableGauge(
			"dispatch.transport.pending_messages",
			observeValues: static () =>
			{
				lock (StateLock)
				{
					return TransportStates.Select(static kvp => new Measurement<long>(
						kvp.Value.PendingMessages,
						new KeyValuePair<string, object?>("transport_name", kvp.Key),
						new KeyValuePair<string, object?>("transport_type", kvp.Value.TransportType)));
				}
			},
			unit: "messages",
			description: "Number of pending messages in transport queue");
	}

	/// <summary>
	/// Records that a message was sent through a transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="messageType">Optional message type.</param>
	public static void RecordMessageSent(string transportName, string transportType, string? messageType = null)
	{
		var tags = new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		};

		if (!string.IsNullOrEmpty(messageType))
		{
			tags.Add("message_type", messageType);
		}

		MessagesSentCounter.Add(1, tags);
	}

	/// <summary>
	/// Records that a message was received through a transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="messageType">Optional message type.</param>
	public static void RecordMessageReceived(string transportName, string transportType, string? messageType = null)
	{
		var tags = new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		};

		if (!string.IsNullOrEmpty(messageType))
		{
			tags.Add("message_type", messageType);
		}

		MessagesReceivedCounter.Add(1, tags);
	}

	/// <summary>
	/// Records a transport error.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="errorType">The type of error (e.g., "timeout", "connection_failed", "serialization_error").</param>
	public static void RecordError(string transportName, string transportType, string errorType)
	{
		ErrorsCounter.Add(1, new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
			{ "error_type", errorType },
		});
	}

	/// <summary>
	/// Records the duration of a send operation.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="durationMs">Duration in milliseconds.</param>
	public static void RecordSendDuration(string transportName, string transportType, double durationMs)
	{
		SendDurationHistogram.Record(durationMs, new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		});
	}

	/// <summary>
	/// Records the duration of a receive operation.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="durationMs">Duration in milliseconds.</param>
	public static void RecordReceiveDuration(string transportName, string transportType, double durationMs)
	{
		ReceiveDurationHistogram.Record(durationMs, new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		});
	}

	/// <summary>
	/// Records that a transport was started.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	public static void RecordTransportStarted(string transportName, string transportType)
	{
		TransportStartsCounter.Add(1, new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		});
	}

	/// <summary>
	/// Records that a transport was stopped.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	public static void RecordTransportStopped(string transportName, string transportType)
	{
		TransportStopsCounter.Add(1, new TagList
		{
			{ "transport_name", transportName },
			{ "transport_type", transportType },
		});
	}

	/// <summary>
	/// Updates the current state metrics for a transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="transportType">The type of transport.</param>
	/// <param name="isConnected">Whether the transport is connected.</param>
	/// <param name="pendingMessages">Number of pending messages.</param>
	public static void UpdateTransportState(
		string transportName,
		string transportType,
		bool isConnected,
		long pendingMessages = 0)
	{
		lock (StateLock)
		{
			TransportStates[transportName] = new TransportMetricsState
			{
				TransportType = transportType,
				IsConnected = isConnected,
				PendingMessages = pendingMessages,
			};
		}
	}

	/// <summary>
	/// Removes a transport from metric tracking.
	/// </summary>
	/// <param name="transportName">The name of the transport to remove.</param>
	public static void RemoveTransport(string transportName)
	{
		lock (StateLock)
		{
			_ = TransportStates.Remove(transportName);
		}
	}

	private sealed class TransportMetricsState
	{
		public required string TransportType { get; init; }

		public bool IsConnected { get; init; }

		public long PendingMessages { get; init; }
	}
}
