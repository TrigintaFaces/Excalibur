// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for configuring <see cref="OutboxOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// Options are validated at <see cref="Build"/> time, ensuring fail-fast behavior.
/// </para>
/// <para>
/// Start with a preset using <see cref="OutboxOptions.HighThroughput"/>,
/// <see cref="OutboxOptions.Balanced"/>, <see cref="OutboxOptions.HighReliability"/>,
/// or <see cref="OutboxOptions.Custom"/>, then apply overrides as needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using a preset with overrides
/// var options = OutboxOptions.HighThroughput()
///     .WithBatchSize(2000)
///     .WithProcessorId("worker-1")
///     .Build();
///
/// // Full custom configuration
/// var customOptions = OutboxOptions.Custom()
///     .WithBatchSize(500)
///     .WithPollingInterval(TimeSpan.FromMilliseconds(500))
///     .WithParallelism(4)
///     .Build();
/// </code>
/// </example>
public interface IOutboxOptionsBuilder
{
	// ========================================
	// Core Settings
	// ========================================

	/// <summary>
	/// Sets the unique identifier for this processor instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="processorId"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The processor ID is used to identify which instance processed a message,
	/// useful for debugging in distributed deployments.
	/// </para>
	/// </remarks>
	IOutboxOptionsBuilder WithProcessorId(string processorId);

	/// <summary>
	/// Sets whether background processing is enabled.
	/// </summary>
	/// <param name="enable">True to enable background processing; otherwise, false.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, a hosted service will be registered that periodically
	/// polls the outbox and publishes pending messages.
	/// </para>
	/// </remarks>
	IOutboxOptionsBuilder EnableBackgroundProcessing(bool enable = true);

	// ========================================
	// Performance Settings
	// ========================================

	/// <summary>
	/// Sets the maximum number of messages to process in a single batch.
	/// </summary>
	/// <param name="batchSize">The batch size. Must be between 1 and 10000.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="batchSize"/> is less than 1 or greater than 10000.
	/// </exception>
	IOutboxOptionsBuilder WithBatchSize(int batchSize);

	/// <summary>
	/// Sets the interval between processing cycles.
	/// </summary>
	/// <param name="interval">The polling interval. Must be at least 10ms.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is less than 10 milliseconds.
	/// </exception>
	IOutboxOptionsBuilder WithPollingInterval(TimeSpan interval);

	/// <summary>
	/// Sets the maximum degree of parallelism and enables parallel processing.
	/// </summary>
	/// <param name="maxDegree">The maximum number of concurrent processing operations. Must be at least 1.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxDegree"/> is less than 1.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Parallel processing can improve throughput but may affect message ordering.
	/// Set to 1 for sequential processing.
	/// </para>
	/// </remarks>
	IOutboxOptionsBuilder WithParallelism(int maxDegree);

	// ========================================
	// Reliability Settings
	// ========================================

	/// <summary>
	/// Sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <param name="maxRetries">The maximum retry count. Must be non-negative.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxRetries"/> is negative.
	/// </exception>
	IOutboxOptionsBuilder WithMaxRetries(int maxRetries);

	/// <summary>
	/// Sets the delay between retry attempts.
	/// </summary>
	/// <param name="delay">The retry delay. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="delay"/> is not positive.
	/// </exception>
	IOutboxOptionsBuilder WithRetryDelay(TimeSpan delay);

	// ========================================
	// Maintenance Settings
	// ========================================

	/// <summary>
	/// Sets the retention period for successfully sent messages before cleanup.
	/// </summary>
	/// <param name="period">The retention period. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="period"/> is not positive.
	/// </exception>
	IOutboxOptionsBuilder WithRetentionPeriod(TimeSpan period);

	/// <summary>
	/// Sets the interval between cleanup cycles.
	/// </summary>
	/// <param name="interval">The cleanup interval. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is not positive.
	/// </exception>
	IOutboxOptionsBuilder WithCleanupInterval(TimeSpan interval);

	/// <summary>
	/// Disables automatic cleanup of old messages.
	/// </summary>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When automatic cleanup is disabled, you are responsible for cleaning up
	/// old messages manually to prevent unbounded storage growth.
	/// </para>
	/// </remarks>
	IOutboxOptionsBuilder DisableAutomaticCleanup();

	// ========================================
	// Terminal Method
	// ========================================

	/// <summary>
	/// Builds the immutable <see cref="OutboxOptions"/> instance.
	/// </summary>
	/// <returns>The configured options.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the configuration is invalid (e.g., RetentionPeriod is less than CleanupInterval).
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method validates all configured settings and returns an immutable
	/// <see cref="OutboxOptions"/> instance. After calling <see cref="Build"/>,
	/// the options cannot be modified.
	/// </para>
	/// </remarks>
	OutboxOptions Build();
}
