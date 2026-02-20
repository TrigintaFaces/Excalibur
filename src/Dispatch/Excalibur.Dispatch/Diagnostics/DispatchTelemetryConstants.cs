// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Constants for OpenTelemetry telemetry names and semantic conventions. Implements R8.21 comprehensive telemetry standards and naming consistency.
/// </summary>
/// <remarks>
/// Centralizes all telemetry naming to ensure consistency across the Dispatch framework. Follows OpenTelemetry semantic conventions where
/// applicable and provides custom conventions for Dispatch-specific operations.
/// </remarks>
public static class DispatchTelemetryConstants
{
	/// <summary>
	/// Base namespace for all Dispatch telemetry.
	/// </summary>
	public const string BaseNamespace = "Excalibur.Dispatch";

	/// <summary>
	/// Core telemetry name shared by both the <see cref="ActivitySources.Core"/> and <see cref="Meters.Core"/> constants.
	/// </summary>
	public const string CoreName = "Excalibur.Dispatch.Core";

	/// <summary>
	/// ActivitySource names for distributed tracing.
	/// </summary>
	public static class ActivitySources
	{
		/// <summary>
		/// Core messaging operations.
		/// </summary>
		public const string Core = CoreName;

		/// <summary>
		/// Pipeline operations.
		/// </summary>
		public const string Pipeline = "Excalibur.Dispatch.Pipeline";

		/// <summary>
		/// Time policy operations.
		/// </summary>
		public const string TimePolicy = "Excalibur.Dispatch.TimePolicy";

		/// <summary>
		/// Batch processor operations.
		/// </summary>
		public const string BatchProcessor = "Excalibur.Dispatch.BatchProcessor";

		/// <summary>
		/// Poison message handler operations.
		/// </summary>
		public const string PoisonMessage = "Excalibur.Dispatch.PoisonMessage";

		/// <summary>
		/// Poison message middleware operations.
		/// </summary>
		public const string PoisonMessageMiddleware = "Excalibur.Dispatch.PoisonMessage.Middleware";

		/// <summary>
		/// Poison message cleanup operations.
		/// </summary>
		public const string PoisonMessageCleanup = "Excalibur.Dispatch.PoisonMessage.Cleanup";

		/// <summary>
		/// Audit logging middleware operations.
		/// </summary>
		public const string AuditLoggingMiddleware = "Excalibur.Dispatch.AuditLoggingMiddleware";

		/// <summary>
		/// Circuit breaker middleware operations.
		/// </summary>
		public const string CircuitBreakerMiddleware = "Excalibur.Dispatch.CircuitBreakerMiddleware";

		/// <summary>
		/// Retry middleware operations.
		/// </summary>
		public const string RetryMiddleware = "Excalibur.Dispatch.RetryMiddleware";

		/// <summary>
		/// Unified batching middleware operations.
		/// </summary>
		public const string UnifiedBatchingMiddleware = "Excalibur.Dispatch.UnifiedBatchingMiddleware";

		/// <summary>
		/// Channel transport common operations.
		/// </summary>
		public const string ChannelTransport = "Excalibur.Dispatch.Transport.Common";

		/// <summary>
		/// Outbox publisher background service operations.
		/// </summary>
		public const string OutboxBackgroundService = "Excalibur.Dispatch.Outbox.Publisher";
	}

	/// <summary>
	/// Meter names for metrics collection.
	/// </summary>
	public static class Meters
	{
		/// <summary>
		/// Core messaging metrics.
		/// </summary>
		public const string Core = CoreName;

		/// <summary>
		/// Pipeline metrics.
		/// </summary>
		public const string Pipeline = "Excalibur.Dispatch.Pipeline";

		/// <summary>
		/// Time policy metrics.
		/// </summary>
		public const string TimePolicy = "Excalibur.Dispatch.TimePolicy";

		/// <summary>
		/// Batch processor metrics.
		/// </summary>
		public const string BatchProcessor = "Excalibur.Dispatch.BatchProcessor";

		/// <summary>
		/// Messaging metrics (MetricsLoggingMiddleware).
		/// </summary>
		public const string Messaging = "Excalibur.Dispatch.Messaging";

		/// <summary>
		/// Channel transport common metrics.
		/// </summary>
		public const string ChannelTransport = "Excalibur.Dispatch.Transport.Common";
	}

	/// <summary>
	/// Activity names for common operations.
	/// </summary>
	public static class Activities
	{
		/// <summary>
		/// Message processing activity.
		/// </summary>
		public const string ProcessMessage = "ProcessMessage";

		/// <summary>
		/// Message storing activity.
		/// </summary>
		public const string StoreMessage = "StoreMessage";

		/// <summary>
		/// Message retrieval activity.
		/// </summary>
		public const string GetMessages = "GetMessages";

		/// <summary>
		/// Schedule storage activity.
		/// </summary>
		public const string StoreSchedule = "StoreSchedule";

		/// <summary>
		/// Schedule retrieval activity.
		/// </summary>
		public const string GetSchedules = "GetSchedules";

		/// <summary>
		/// Schedule completion activity.
		/// </summary>
		public const string CompleteSchedule = "CompleteSchedule";

		/// <summary>
		/// Cleanup completed activity.
		/// </summary>
		public const string CleanupCompleted = "CleanupCompleted";

		/// <summary>
		/// Cleanup failed activity.
		/// </summary>
		public const string CleanupFailed = "CleanupFailed";

		/// <summary>
		/// Batch processing activity.
		/// </summary>
		public const string BatchProcess = "BatchProcess";

		/// <summary>
		/// Bulk storage activity.
		/// </summary>
		public const string BulkStore = "BulkStore";
	}

	/// <summary>
	/// Tag names for telemetry attributes.
	/// </summary>
	public static class Tags
	{
		/// <summary>
		/// Unique identifier of the message.
		/// </summary>
		public const string MessageId = "message.id";

		/// <summary>
		/// Type classification of the message.
		/// </summary>
		public const string MessageType = "message.type";

		/// <summary>
		/// Name identifier of the message.
		/// </summary>
		public const string MessageName = "message.name";

		/// <summary>
		/// Size of the message payload in bytes.
		/// </summary>
		public const string MessageSize = "message.size";

		/// <summary>
		/// Tenant identifier associated with the message.
		/// </summary>
		public const string MessageTenant = "message.tenant";

		/// <summary>
		/// Unique identifier of the schedule.
		/// </summary>
		public const string ScheduleId = "schedule.id";

		/// <summary>
		/// Whether the schedule is enabled or disabled.
		/// </summary>
		public const string ScheduleEnabled = "schedule.enabled";

		/// <summary>
		/// Type classification of the schedule.
		/// </summary>
		public const string ScheduleType = "schedule.type";

		/// <summary>
		/// Number of items in the schedule.
		/// </summary>
		public const string ScheduleCount = "schedule.count";

		/// <summary>
		/// Type classification of the operation being performed.
		/// </summary>
		public const string OperationType = "operation.type";

		/// <summary>
		/// Result status of the operation (success, failure, etc.).
		/// </summary>
		public const string OperationResult = "operation.result";

		/// <summary>
		/// Current stage or phase of the operation.
		/// </summary>
		public const string OperationStage = "operation.stage";

		/// <summary>
		/// Type classification of the data store.
		/// </summary>
		public const string StoreType = "store.type";

		/// <summary>
		/// Name identifier of the data store.
		/// </summary>
		public const string StoreName = "store.name";

		/// <summary>
		/// Whether a cache lookup resulted in a hit or miss.
		/// </summary>
		public const string CacheHit = "cache.hit";

		/// <summary>
		/// Size of the processing batch.
		/// </summary>
		public const string BatchSize = "batch.size";

		/// <summary>
		/// Whether the message is a duplicate.
		/// </summary>
		public const string IsDuplicate = "message.is_duplicate";

		/// <summary>
		/// Classification type of the error that occurred.
		/// </summary>
		public const string ErrorType = "error.type";

		/// <summary>
		/// Descriptive message about the error.
		/// </summary>
		public const string ErrorMessage = "error.message";

		/// <summary>
		/// Whether the error condition is retryable.
		/// </summary>
		public const string IsRetryable = "error.retryable";
	}

	/// <summary>
	/// Standard tag values for common scenarios.
	/// </summary>
	public static class TagValues
	{
		/// <summary>
		/// Operation completed successfully.
		/// </summary>
		public const string Success = "success";

		/// <summary>
		/// Operation failed with an error.
		/// </summary>
		public const string Failure = "failure";

		/// <summary>
		/// Operation exceeded timeout limit.
		/// </summary>
		public const string Timeout = "timeout";

		/// <summary>
		/// Operation was cancelled before completion.
		/// </summary>
		public const string Cancelled = "cancelled";

		/// <summary>
		/// Inbox message store type.
		/// </summary>
		public const string InboxStore = "inbox";

		/// <summary>
		/// Outbox message store type.
		/// </summary>
		public const string OutboxStore = "outbox";

		/// <summary>
		/// Schedule store type.
		/// </summary>
		public const string ScheduleStore = "schedule";

		/// <summary>
		/// Store operation type.
		/// </summary>
		public const string Store = "store";

		/// <summary>
		/// Retrieve operation type.
		/// </summary>
		public const string Retrieve = "retrieve";

		/// <summary>
		/// Update operation type.
		/// </summary>
		public const string Update = "update";

		/// <summary>
		/// Delete operation type.
		/// </summary>
		public const string Delete = "delete";

		/// <summary>
		/// Cleanup operation type.
		/// </summary>
		public const string Cleanup = "cleanup";

		/// <summary>
		/// Cache hit state.
		/// </summary>
		public const string Hit = "hit";

		/// <summary>
		/// Cache miss state.
		/// </summary>
		public const string Miss = "miss";

		/// <summary>
		/// Cache evicted state.
		/// </summary>
		public const string Evicted = "evicted";
	}
}
