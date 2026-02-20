// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Diagnostics;

/// <summary>
/// Constants for transport-layer OpenTelemetry telemetry names and semantic conventions.
/// Extends the <c>DispatchTelemetryConstants</c> naming standard with transport-specific conventions.
/// </summary>
/// <remarks>
/// <para>
/// Meter names follow the established <c>Excalibur.Dispatch.{Component}</c> pattern:
/// <c>Excalibur.Dispatch.Transport.Kafka</c>, <c>Excalibur.Dispatch.Transport.RabbitMQ</c>, etc.
/// </para>
/// <para>
/// Metric names follow the <c>dispatch.transport.*</c> prefix with dot notation.
/// Tag names use dot notation consistent with the core framework.
/// </para>
/// </remarks>
public static class TransportTelemetryConstants
{
	/// <summary>
	/// Gets the Meter name for a specific transport.
	/// </summary>
	/// <param name="transportName">The transport name (e.g., "Kafka", "RabbitMQ", "AzureServiceBus").</param>
	/// <returns>The meter name in the format <c>Excalibur.Dispatch.Transport.{transportName}</c>.</returns>
	public static string MeterName(string transportName) => $"Excalibur.Dispatch.Transport.{transportName}";

	/// <summary>
	/// Gets the ActivitySource name for a specific transport.
	/// </summary>
	/// <param name="transportName">The transport name (e.g., "Kafka", "RabbitMQ", "AzureServiceBus").</param>
	/// <returns>The activity source name in the format <c>Excalibur.Dispatch.Transport.{transportName}</c>.</returns>
	public static string ActivitySourceName(string transportName) => $"Excalibur.Dispatch.Transport.{transportName}";

	/// <summary>
	/// Standard metric names for transport operations.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>
		/// Counter: total messages sent successfully.
		/// </summary>
		public const string MessagesSent = "dispatch.transport.messages.sent";

		/// <summary>
		/// Counter: total message send failures.
		/// </summary>
		public const string MessagesSendFailed = "dispatch.transport.messages.send_failed";

		/// <summary>
		/// Counter: total messages received.
		/// </summary>
		public const string MessagesReceived = "dispatch.transport.messages.received";

		/// <summary>
		/// Counter: total messages acknowledged.
		/// </summary>
		public const string MessagesAcknowledged = "dispatch.transport.messages.acknowledged";

		/// <summary>
		/// Counter: total messages rejected.
		/// </summary>
		public const string MessagesRejected = "dispatch.transport.messages.rejected";

		/// <summary>
		/// Counter: total messages routed to dead letter queue.
		/// </summary>
		public const string MessagesDeadLettered = "dispatch.transport.messages.dead_lettered";

		/// <summary>
		/// Histogram: duration of send operations in milliseconds.
		/// </summary>
		public const string SendDuration = "dispatch.transport.send.duration";

		/// <summary>
		/// Histogram: duration of receive operations in milliseconds.
		/// </summary>
		public const string ReceiveDuration = "dispatch.transport.receive.duration";

		/// <summary>
		/// Histogram: number of messages in a batch operation.
		/// </summary>
		public const string BatchSize = "dispatch.transport.batch.size";

		/// <summary>
		/// Counter: total messages requeued for redelivery.
		/// </summary>
		public const string MessagesRequeued = "dispatch.transport.messages.requeued";

		/// <summary>
		/// Counter: total handler errors during subscriber message processing.
		/// </summary>
		public const string HandlerErrors = "dispatch.transport.handler.errors";

		/// <summary>
		/// Histogram: duration of subscriber handler invocations in milliseconds.
		/// </summary>
		public const string HandlerDuration = "dispatch.transport.handler.duration";
	}

	/// <summary>
	/// Standard tag names for transport telemetry.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>
		/// The transport provider name (e.g., "Kafka", "AzureServiceBus").
		/// </summary>
		public const string TransportName = "dispatch.transport.name";

		/// <summary>
		/// The destination queue or topic name.
		/// </summary>
		public const string Destination = "dispatch.transport.destination";

		/// <summary>
		/// The source queue or subscription name.
		/// </summary>
		public const string Source = "dispatch.transport.source";

		/// <summary>
		/// The operation being performed (e.g., "send", "receive", "acknowledge").
		/// </summary>
		public const string Operation = "dispatch.transport.operation";

		/// <summary>
		/// The message type.
		/// </summary>
		public const string MessageType = "message.type";

		/// <summary>
		/// The error type.
		/// </summary>
		public const string ErrorType = "error.type";

		/// <summary>
		/// Whether the error is retryable.
		/// </summary>
		public const string IsRetryable = "error.retryable";
	}

	/// <summary>
	/// Well-known property keys set by decorators and read by transport implementations.
	/// These keys are used in <see cref="TransportMessage.Properties"/> to pass transport-specific hints
	/// without polluting the core interface.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class PropertyKeys
	{
		/// <summary>
		/// Ordering key for message ordering (maps to Kafka message key, AWS MessageGroupId, Azure SessionId, Google ordering key).
		/// </summary>
		public const string OrderingKey = "dispatch.ordering.key";

		/// <summary>
		/// Partition key for message routing (maps to Kafka partition, Azure PartitionKey).
		/// </summary>
		public const string PartitionKey = "dispatch.partition.key";

		/// <summary>
		/// Deduplication identifier (maps to AWS MessageDeduplicationId, Azure MessageId for dedup).
		/// </summary>
		public const string DeduplicationId = "dispatch.deduplication.id";

		/// <summary>
		/// Scheduled delivery time as an ISO 8601 string (maps to Azure ScheduledEnqueueTime, Google publish time).
		/// </summary>
		public const string ScheduledTime = "dispatch.scheduled.time";

		/// <summary>
		/// Message priority level (maps to RabbitMQ BasicProperties.Priority).
		/// </summary>
		public const string Priority = "dispatch.priority";

		/// <summary>
		/// Delay in seconds before message becomes visible (maps to AWS DelaySeconds).
		/// </summary>
		public const string DelaySeconds = "dispatch.delay.seconds";
	}
}
