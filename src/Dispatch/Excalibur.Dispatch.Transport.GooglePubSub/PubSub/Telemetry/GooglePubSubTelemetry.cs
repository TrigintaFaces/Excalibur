// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Telemetry constants for Google Pub/Sub.
/// </summary>
/// <remarks>
/// The <see cref="ActivitySourceName"/> and <see cref="Version"/> constants delegate to
/// <see cref="GooglePubSubTelemetryConstants"/> for consistency across all Google Pub/Sub components.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
	Justification = "Nested static classes (Tags, TelemetryMetrics) are intentionally used to group related telemetry constants, following established OpenTelemetry naming conventions and patterns.")]
public static class GooglePubSubTelemetry
{
	/// <summary>
	/// Activity source name for Google Pub/Sub operations.
	/// </summary>
	public const string ActivitySourceName = GooglePubSubTelemetryConstants.ActivitySourceName;

	/// <summary>
	/// Version of the telemetry implementation.
	/// </summary>
	public const string Version = GooglePubSubTelemetryConstants.Version;

	/// <summary>
	/// Tag names for OpenTelemetry.
	/// </summary>
	public static class Tags
	{
		/// <summary>
		/// Message ID tag.
		/// </summary>
		public const string MessageId = "messaging.message_id";

		/// <summary>
		/// Ordering key tag.
		/// </summary>
		public const string OrderingKey = "messaging.ordering_key";

		/// <summary>
		/// Worker ID tag.
		/// </summary>
		public const string WorkerId = "messaging.worker_id";

		/// <summary>
		/// Subscription name tag.
		/// </summary>
		public const string Subscription = "messaging.destination";

		/// <summary>
		/// Topic name tag.
		/// </summary>
		public const string Topic = "messaging.destination_kind";

		/// <summary>
		/// Project ID tag.
		/// </summary>
		public const string ProjectId = "gcp.project_id";

		/// <summary>
		/// Batch size tag.
		/// </summary>
		public const string BatchSize = "messaging.batch.message_count";

		/// <summary>
		/// Error type tag.
		/// </summary>
		public const string ErrorType = "error.type";
	}

	/// <summary>
	/// Metric names.
	/// </summary>
	public static class TelemetryMetrics
	{
		/// <summary>
		/// Messages enqueued metric.
		/// </summary>
		public const string MessagesEnqueued = "pubsub.messages.enqueued";

		/// <summary>
		/// Messages processed metric.
		/// </summary>
		public const string MessagesProcessed = "pubsub.messages.processed";

		/// <summary>
		/// Messages failed metric.
		/// </summary>
		public const string MessagesFailed = "pubsub.messages.failed";

		/// <summary>
		/// Queue time histogram metric.
		/// </summary>
		public const string QueueTime = "pubsub.message.queue_time";

		/// <summary>
		/// Processing time histogram metric.
		/// </summary>
		public const string ProcessingTime = "pubsub.message.processing_time";

		/// <summary>
		/// Worker utilization gauge.
		/// </summary>
		public const string WorkerUtilization = "pubsub.worker.utilization";

		/// <summary>
		/// Active workers gauge.
		/// </summary>
		public const string ActiveWorkers = "pubsub.worker.active_count";
	}
}
