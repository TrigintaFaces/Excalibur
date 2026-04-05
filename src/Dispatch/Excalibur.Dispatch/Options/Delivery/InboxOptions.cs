// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Provides configuration options for inbox pattern implementations, controlling message processing behavior, performance characteristics,
/// and reliability features. These options enable fine-tuning of inbox processors for different deployment scenarios and performance requirements.
/// </summary>
/// <remarks>
/// Inbox options control various aspects of the inbox pattern implementation including batching behavior, deduplication handling, retry
/// policies, and performance optimizations. Proper configuration of these options is essential for achieving optimal performance and
/// reliability in high-throughput messaging scenarios. The options include validation logic to ensure consistent and valid configuration values.
/// </remarks>
public sealed class InboxOptions
{
	/// <summary>
	/// Gets or sets the behavior when duplicate messages are encountered during inbox processing. This setting controls how the system
	/// responds to messages that have already been processed.
	/// </summary>
	/// <value> A <see cref="SkipBehavior" /> value indicating how to handle duplicate messages. The default value is <see cref="SkipBehavior.Silent" />. </value>
	/// <remarks>
	/// The duplicate behavior setting affects both performance and observability of the inbox system. Silent skipping provides the best
	/// performance but may hide potential issues with message producers. Other behaviors may log warnings or take additional actions to
	/// improve system visibility.
	/// </remarks>
	public SkipBehavior DuplicateBehavior { get; set; } = SkipBehavior.Silent;

	/// <summary>
	/// Gets or sets the maximum number of processing attempts for failed messages.
	/// </summary>
	/// <value> An integer representing the maximum number of times a failed message will be retried. Must be greater than zero. </value>
	[Range(1, int.MaxValue)]
	public int MaxAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets the default time-to-live for messages in the inbox before they expire.
	/// </summary>
	/// <value>
	/// A nullable <see cref="TimeSpan" /> representing the message expiration time, or <c> null </c> if messages should not expire by default.
	/// </value>
	public TimeSpan? DefaultMessageTimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the deduplication options for inbox message processing.
	/// </summary>
	/// <value>
	/// The deduplication options for inbox message processing.
	/// </value>
	public DeduplicationOptions Deduplication { get; set; } = new();

	/// <summary>
	/// Gets or sets the capacity and throughput options controlling queue sizes, batch sizes, and parallelism.
	/// </summary>
	/// <value>The capacity options.</value>
	public InboxCapacityOptions Capacity { get; set; } = new();

	/// <summary>
	/// Gets or sets the batch tuning options controlling database batching, timeouts, and dynamic sizing.
	/// </summary>
	/// <value>The batch tuning options.</value>
	public InboxBatchTuningOptions BatchTuning { get; set; } = new();

	/// <summary>
	/// Validates the configured option values.
	/// </summary>
	/// <param name="options"> The options instance to validate. </param>
	/// <returns> An error message if validation fails; otherwise <c> null </c>. </returns>
	public static string? Validate(InboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Capacity.QueueCapacity <= 0)
		{
			return "Capacity.QueueCapacity must be greater than zero.";
		}

		if (options.Capacity.ProducerBatchSize <= 0)
		{
			return "Capacity.ProducerBatchSize must be greater than zero.";
		}

		if (options.Capacity.ConsumerBatchSize <= 0)
		{
			return "Capacity.ConsumerBatchSize must be greater than zero.";
		}

		if (options.Capacity.PerRunTotal <= 0)
		{
			return "Capacity.PerRunTotal must be greater than zero.";
		}

		if (options.MaxAttempts <= 0)
		{
			return "MaxAttempts must be greater than zero.";
		}

		if (options.Capacity.QueueCapacity < options.Capacity.ProducerBatchSize)
		{
			return "Capacity.QueueCapacity cannot be less than the Capacity.ProducerBatchSize.";
		}

		if (options.Capacity.ParallelProcessingDegree <= 0)
		{
			return "Capacity.ParallelProcessingDegree must be greater than zero.";
		}

		if (options.BatchTuning.EnableDynamicBatchSizing)
		{
			if (options.BatchTuning.MinBatchSize <= 0)
			{
				return "BatchTuning.MinBatchSize must be greater than zero when dynamic batch sizing is enabled.";
			}

			if (options.BatchTuning.MaxBatchSize <= 0)
			{
				return "BatchTuning.MaxBatchSize must be greater than zero when dynamic batch sizing is enabled.";
			}

			if (options.BatchTuning.MinBatchSize > options.BatchTuning.MaxBatchSize)
			{
				return "BatchTuning.MinBatchSize cannot be greater than BatchTuning.MaxBatchSize.";
			}
		}

		if (options.BatchTuning.BatchProcessingTimeout <= TimeSpan.Zero)
		{
			return "BatchTuning.BatchProcessingTimeout must be greater than zero.";
		}

		return null;
	}
}

/// <summary>
/// Configuration options for inbox capacity and throughput, controlling queue sizes, batch sizes, and parallelism.
/// </summary>
public sealed class InboxCapacityOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to process in a single execution run. This setting provides backpressure control and
	/// prevents runaway message processing.
	/// </summary>
	/// <value> An integer representing the maximum number of messages to process per run. Must be greater than zero. </value>
	[Range(1, int.MaxValue)]
	public int PerRunTotal { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the capacity of the internal message processing queue. This setting controls memory usage and buffering behavior during
	/// message processing.
	/// </summary>
	/// <value>
	/// An integer representing the maximum number of messages that can be held in the processing queue. Must be greater than zero and at
	/// least as large as <see cref="ProducerBatchSize" />.
	/// </value>
	[Range(1, int.MaxValue)]
	public int QueueCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the batch size for loading messages from storage into the processing queue.
	/// </summary>
	/// <value>
	/// An integer representing the number of messages to load in each batch from storage. Must be greater than zero.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ProducerBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch size for processing messages from the internal queue.
	/// </summary>
	/// <value> An integer representing the number of messages to process together in each batch. Must be greater than zero. </value>
	[Range(1, int.MaxValue)]
	public int ConsumerBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the degree of parallelism for batch processing. Default is 1 (sequential processing).
	/// </summary>
	/// <value>The current <see cref="ParallelProcessingDegree"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ParallelProcessingDegree { get; set; } = 1;
}

/// <summary>
/// Configuration options for inbox batch tuning, controlling database batching, timeouts, and dynamic sizing.
/// </summary>
public sealed class InboxBatchTuningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable batch database operations.
	/// </summary>
	/// <value>The current <see cref="EnableBatchDatabaseOperations"/> value.</value>
	public bool EnableBatchDatabaseOperations { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for processing a batch of messages.
	/// </summary>
	/// <value>
	/// The timeout for processing a batch of messages.
	/// </value>
	public TimeSpan BatchProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable dynamic batch sizing based on throughput.
	/// </summary>
	/// <value>The current <see cref="EnableDynamicBatchSizing"/> value.</value>
	public bool EnableDynamicBatchSizing { get; set; }

	/// <summary>
	/// Gets or sets the minimum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The current <see cref="MinBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MinBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum batch size when dynamic sizing is enabled.
	/// </summary>
	/// <value>The current <see cref="MaxBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 1000;
}
