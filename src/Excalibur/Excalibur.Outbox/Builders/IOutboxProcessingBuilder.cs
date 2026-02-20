// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for configuring outbox processing settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures how messages are retrieved and processed from the outbox store.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// outbox.WithProcessing(processing =>
/// {
///     processing.BatchSize(100)
///               .PollingInterval(TimeSpan.FromSeconds(5))
///               .MaxRetryCount(5)
///               .RetryDelay(TimeSpan.FromMinutes(1));
/// });
/// </code>
/// </example>
public interface IOutboxProcessingBuilder
{
	/// <summary>
	/// Sets the maximum number of messages to process in a single batch.
	/// </summary>
	/// <param name="size">The batch size. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="size"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Larger batch sizes can improve throughput but may increase memory usage
	/// and processing time per batch. Default is 100.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder BatchSize(int size);

	/// <summary>
	/// Sets the interval between processing cycles.
	/// </summary>
	/// <param name="interval">The polling interval. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Shorter intervals reduce latency but increase database load.
	/// Default is 5 seconds.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder PollingInterval(TimeSpan interval);

	/// <summary>
	/// Sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <param name="count">The maximum retry count. Must be greater than or equal to 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// After the maximum retries are exhausted, messages are moved to the dead letter queue
	/// if one is configured. Default is 3.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder MaxRetryCount(int count);

	/// <summary>
	/// Sets the delay between retry attempts.
	/// </summary>
	/// <param name="delay">The retry delay. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="delay"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Consider using exponential backoff for transient failures.
	/// Default is 5 minutes.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder RetryDelay(TimeSpan delay);

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
	/// useful for debugging in distributed deployments. If not set, a GUID is generated.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder ProcessorId(string processorId);

	/// <summary>
	/// Enables parallel processing of messages within a batch.
	/// </summary>
	/// <param name="maxDegreeOfParallelism">
	/// The maximum number of concurrent message processing operations. Default is 4.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxDegreeOfParallelism"/> is less than 1.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Parallel processing can improve throughput but may affect message ordering.
	/// Only enable if message order is not critical or if you have partitioning in place.
	/// </para>
	/// </remarks>
	IOutboxProcessingBuilder EnableParallelProcessing(int maxDegreeOfParallelism = 4);
}
