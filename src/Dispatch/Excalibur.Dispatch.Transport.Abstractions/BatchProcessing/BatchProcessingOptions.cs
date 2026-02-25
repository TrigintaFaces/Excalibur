// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for batch processing.
/// </summary>
public sealed class BatchProcessingOptions
{
	/// <summary>
	/// Gets or sets the maximum batch size.
	/// </summary>
	/// <value>The upper bound on the number of messages processed in a single batch.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch timeout.
	/// </summary>
	/// <value>The <see cref="TimeSpan"/> allowed for collecting items before dispatch.</value>
	public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to process messages in parallel within a batch.
	/// </summary>
	/// <value><see langword="true"/> to schedule messages concurrently; otherwise <see langword="false"/>.</value>
	public bool ProcessInParallel { get; set; } = true;

	/// <summary>
	/// Gets or sets the degree of parallelism.
	/// </summary>
	/// <value>The maximum number of concurrent workers allowed for a batch.</value>
	[Range(1, int.MaxValue)]
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets a value indicating whether to continue on errors.
	/// </summary>
	/// <value><see langword="true"/> to record failures but continue processing; otherwise <see langword="false"/>.</value>
	public bool ContinueOnError { get; set; } = true;

	/// <summary>
	/// Gets or sets the retry policy.
	/// </summary>
	/// <value>The policy describing retry behavior when processing failures occur.</value>
	public RetryPolicy RetryPolicy { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value><see langword="true"/> to publish processing metrics; otherwise <see langword="false"/>.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter queue for failed messages.
	/// </summary>
	/// <value><see langword="true"/> to route failed messages to the configured dead letter queue.</value>
	public bool EnableDeadLetter { get; set; } = true;

	/// <summary>
	/// Gets or sets the batch completion strategy.
	/// </summary>
	/// <value>The strategy used to determine when the batch is ready to be processed.</value>
	public BatchCompletionStrategy CompletionStrategy { get; set; } = BatchCompletionStrategy.Size;

	/// <summary>
	/// Gets or sets the minimum batch size for processing.
	/// </summary>
	/// <value>The number of messages that must be collected before a batch can be dispatched.</value>
	[Range(1, int.MaxValue)]
	public int MinBatchSize { get; set; } = 1;

	/// <summary>
	/// Gets or sets the batch collection timeout.
	/// </summary>
	/// <value>The <see cref="TimeSpan"/> allowed for aggregating messages before a partial dispatch.</value>
	public TimeSpan CollectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to preserve message order.
	/// </summary>
	/// <value><see langword="true"/> to maintain message ordering semantics; otherwise <see langword="false"/>.</value>
	public bool PreserveOrder { get; set; }

	/// <summary>
	/// Gets or sets the batch priority.
	/// </summary>
	/// <value>The relative priority assigned to newly created batches.</value>
	public BatchPriority DefaultPriority { get; set; } = BatchPriority.Normal;
}
