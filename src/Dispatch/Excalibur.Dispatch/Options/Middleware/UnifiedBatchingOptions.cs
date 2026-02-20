// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for the UnifiedBatchingMiddleware.
/// </summary>
public sealed class UnifiedBatchingOptions
{
	/// <summary>
	/// Gets or sets maximum number of messages to batch together.
	/// </summary>
	/// <value>The current <see cref="MaxBatchSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 32;

	/// <summary>
	/// Gets or sets maximum time to wait before flushing a batch.
	/// </summary>
	/// <value>
	/// Maximum time to wait before flushing a batch.
	/// </value>
	public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// Gets or sets maximum degree of parallelism for individual message processing.
	/// </summary>
	/// <value>The current <see cref="MaxParallelism"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxParallelism { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets a value indicating whether to process batches as optimized bulk operations when possible.
	/// </summary>
	/// <value>The current <see cref="ProcessAsOptimizedBulk"/> value.</value>
	public bool ProcessAsOptimizedBulk { get; set; } = true;

	/// <summary>
	/// Gets set of message types that should not be batched.
	/// </summary>
	/// <value>The current <see cref="NonBatchableMessageTypes"/> value.</value>
	public HashSet<Type> NonBatchableMessageTypes { get; } = [];

	/// <summary>
	/// Gets or sets custom filter function to determine if a message should be batched. If null, uses NonBatchableMessageTypes for filtering.
	/// </summary>
	/// <value>The current <see cref="BatchFilter"/> value.</value>
	public Func<IDispatchMessage, bool>? BatchFilter { get; set; }

	/// <summary>
	/// Gets or sets custom function to determine the batch key for grouping messages. If null, groups by message type name.
	/// </summary>
	/// <value>The current <see cref="BatchKeySelector"/> value.</value>
	public Func<IDispatchMessage, string>? BatchKeySelector { get; set; }
}
