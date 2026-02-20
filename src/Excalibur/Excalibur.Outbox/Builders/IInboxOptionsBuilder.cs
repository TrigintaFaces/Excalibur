// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for constructing immutable <see cref="InboxOptions"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern. Use one of
/// the factory methods on <see cref="InboxOptions"/> to create a builder initialized with
/// a preset, then chain override methods as needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Preset-based (recommended)
/// var options = InboxOptions.HighThroughput().Build();
///
/// // Preset with overrides
/// var options = InboxOptions.Balanced()
///     .WithQueueCapacity(1000)
///     .WithParallelism(8)
///     .Build();
///
/// // Full custom
/// var options = InboxOptions.Custom()
///     .WithQueueCapacity(500)
///     .WithProducerBatchSize(100)
///     .WithConsumerBatchSize(50)
///     .WithMaxAttempts(7)
///     .Build();
/// </code>
/// </example>
public interface IInboxOptionsBuilder
{
	/// <summary>
	/// Sets the capacity of the internal message processing queue.
	/// </summary>
	/// <param name="capacity">The maximum number of messages that can be held in the queue.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is less than 1.</exception>
	IInboxOptionsBuilder WithQueueCapacity(int capacity);

	/// <summary>
	/// Sets the batch size for loading messages from storage.
	/// </summary>
	/// <param name="batchSize">The number of messages to load per batch.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="batchSize"/> is less than 1.</exception>
	IInboxOptionsBuilder WithProducerBatchSize(int batchSize);

	/// <summary>
	/// Sets the batch size for processing messages.
	/// </summary>
	/// <param name="batchSize">The number of messages to process together.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="batchSize"/> is less than 1.</exception>
	IInboxOptionsBuilder WithConsumerBatchSize(int batchSize);

	/// <summary>
	/// Sets the maximum number of messages to process per run.
	/// </summary>
	/// <param name="total">The maximum messages per run.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="total"/> is less than 1.</exception>
	IInboxOptionsBuilder WithPerRunTotal(int total);

	/// <summary>
	/// Sets the maximum number of processing attempts for failed messages.
	/// </summary>
	/// <param name="maxAttempts">The maximum retry attempts.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxAttempts"/> is less than 1.</exception>
	IInboxOptionsBuilder WithMaxAttempts(int maxAttempts);

	/// <summary>
	/// Sets the degree of parallelism for batch processing.
	/// </summary>
	/// <param name="maxDegree">The maximum parallel processing degree.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxDegree"/> is less than 1.</exception>
	IInboxOptionsBuilder WithParallelism(int maxDegree);

	/// <summary>
	/// Sets the timeout for processing a batch of messages.
	/// </summary>
	/// <param name="timeout">The processing timeout.</param>
	/// <returns>This builder for method chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is not positive.</exception>
	IInboxOptionsBuilder WithBatchProcessingTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the default time-to-live for messages.
	/// </summary>
	/// <param name="ttl">The message TTL, or null for no expiration.</param>
	/// <returns>This builder for method chaining.</returns>
	IInboxOptionsBuilder WithDefaultMessageTtl(TimeSpan? ttl);

	/// <summary>
	/// Enables dynamic batch sizing based on throughput.
	/// </summary>
	/// <param name="minBatchSize">The minimum batch size.</param>
	/// <param name="maxBatchSize">The maximum batch size.</param>
	/// <returns>This builder for method chaining.</returns>
	IInboxOptionsBuilder EnableDynamicBatchSizing(int minBatchSize = 10, int maxBatchSize = 1000);

	/// <summary>
	/// Disables batch database operations (use single-message operations).
	/// </summary>
	/// <returns>This builder for method chaining.</returns>
	IInboxOptionsBuilder DisableBatchDatabaseOperations();

	/// <summary>
	/// Builds the immutable <see cref="InboxOptions"/> instance.
	/// </summary>
	/// <returns>The configured inbox options.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the configuration is invalid.</exception>
	InboxOptions Build();
}
