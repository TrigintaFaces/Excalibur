// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead configuration options.
/// </summary>
public sealed class BulkheadOptions
{
	/// <summary>
	/// Gets or sets the maximum number of concurrent operations allowed.
	/// </summary>
	/// <value>The upper bound on simultaneous operations permitted before requests are queued. Defaults to 10.</value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of operations that can be queued.
	/// </summary>
	/// <value>The maximum queue depth allowed when concurrency is exhausted. Defaults to 50.</value>
	[Range(0, int.MaxValue)]
	public int MaxQueueLength { get; set; } = 50;

	/// <summary>
	/// Gets or sets the timeout for individual operations.
	/// </summary>
	/// <value>The duration after which an in-flight operation is cancelled. Defaults to 30 seconds.</value>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to allow queueing when at capacity.
	/// </summary>
	/// <value><see langword="true"/> to enqueue requests after the concurrency limit is reached; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool AllowQueueing { get; set; } = true;

	/// <summary>
	/// Gets or sets the priority selector for queued operations.
	/// </summary>
	/// <value>A delegate that assigns priority to queued work items, or <see langword="null"/> to use FIFO ordering.</value>
	public Func<object?, int>? PrioritySelector { get; set; }
}
