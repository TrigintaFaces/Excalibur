// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// High-performance logging for common AWS hot paths across all services.
/// </summary>
internal static partial class AwsCommonLogging
{
	// Source-generated logging methods

	/// <summary>
	/// Connection pooling hot path logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.ConnectionAcquired, LogLevel.Debug,
		"Connection acquired from pool {PoolName}, {AvailableConnections} available")]
	public static partial void LogConnectionAcquired(this ILogger logger, string poolName, int availableConnections);

	[LoggerMessage(AwsSqsEventId.ConnectionReleased, LogLevel.Debug,
		"Connection released to pool {PoolName}")]
	public static partial void LogConnectionReleased(this ILogger logger, string poolName);

	[LoggerMessage(AwsSqsEventId.ConnectionPoolExhausted, LogLevel.Warning,
		"Connection pool {PoolName} exhausted, {WaitingRequests} requests waiting")]
	public static partial void LogConnectionPoolExhausted(this ILogger logger, string poolName, int waitingRequests);

	/// <summary>
	/// Retry and circuit breaker logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.RetryAttempt, LogLevel.Debug,
		"Retry attempt {Attempt}/{MaxAttempts} for operation {Operation}")]
	public static partial void LogRetryAttempt(this ILogger logger, string operation, int attempt, int maxAttempts);

	[LoggerMessage(AwsSqsEventId.CircuitBreakerOpened, LogLevel.Warning,
		"Circuit breaker opened for {ServiceName}")]
	public static partial void LogCircuitBreakerOpened(this ILogger logger, string serviceName);

	[LoggerMessage(AwsSqsEventId.CircuitBreakerClosed, LogLevel.Information,
		"Circuit breaker closed for {ServiceName}")]
	public static partial void LogCircuitBreakerClosed(this ILogger logger, string serviceName);

	/// <summary>
	/// Batch operation logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.BatchOperationStarted, LogLevel.Debug,
		"Batch operation {Operation} started with {ItemCount} items")]
	public static partial void LogBatchOperationStarted(this ILogger logger, string operation, int itemCount);

	[LoggerMessage(AwsSqsEventId.BatchOperationCompleted, LogLevel.Debug,
		"Batch operation {Operation} completed: {SuccessCount} succeeded, {FailureCount} failed")]
	public static partial void LogBatchOperationCompleted(this ILogger logger, string operation, int successCount, int failureCount);

	/// <summary>
	/// Session management logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.SessionCreated, LogLevel.Debug,
		"Session {SessionId} created for {ServiceName}")]
	public static partial void LogSessionCreated(this ILogger logger, string sessionId, string serviceName);

	[LoggerMessage(AwsSqsEventId.SessionExpired, LogLevel.Debug,
		"Session {SessionId} expired")]
	public static partial void LogSessionExpired(this ILogger logger, string sessionId);

	/// <summary>
	/// DLQ processing logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.DlqMessageProcessed, LogLevel.Information,
		"DLQ message {MessageId} processed after {RetryCount} retries")]
	public static partial void LogDlqMessageProcessed(this ILogger logger, string messageId, int retryCount);

	[LoggerMessage(AwsSqsEventId.DlqMessageFailed, LogLevel.Error,
		"DLQ message {MessageId} failed permanently: {Reason}")]
	public static partial void LogDlqMessageFailed(this ILogger logger, string messageId, string reason, Exception ex);

	/// <summary>
	/// Metrics collection logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.MetricRecorded, LogLevel.Trace,
		"Metric {MetricName} recorded: {Value}")]
	public static partial void LogMetricRecorded(this ILogger logger, string metricName, double value);

	[LoggerMessage(AwsSqsEventId.MetricsBatchPublished, LogLevel.Debug,
		"Metrics batch published with {MetricCount} metrics")]
	public static partial void LogMetricsBatchPublished(this ILogger logger, int metricCount);

	/// <summary>
	/// SNS-specific logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.SnsMessagePublished, LogLevel.Debug,
		"SNS message {MessageId} published to topic {TopicArn}")]
	public static partial void LogSnsMessagePublished(this ILogger logger, string messageId, string topicArn);

	[LoggerMessage(AwsSqsEventId.SnsTopicCreated, LogLevel.Debug,
		"SNS batch of {MessageCount} messages published to topic {TopicArn}")]
	public static partial void LogSnsBatchPublished(this ILogger logger, string topicArn, int messageCount);

	/// <summary>
	/// EventBridge-specific logs.
	/// </summary>
	[LoggerMessage(AwsSqsEventId.EventBridgeEventPublished, LogLevel.Debug,
		"EventBridge event {EventId} published to bus {EventBusName}")]
	public static partial void LogEventBridgeEventPublished(this ILogger logger, string eventId, string eventBusName);

	[LoggerMessage(AwsSqsEventId.EventBridgeRuleCreated, LogLevel.Debug,
		"EventBridge batch of {EventCount} events published to bus {EventBusName}")]
	public static partial void LogEventBridgeBatchPublished(this ILogger logger, string eventBusName, int eventCount);
}
