// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Middleware.ParallelExecution;

/// <summary>
/// Configuration options for parallel event handler execution.
/// </summary>
/// <remarks>
/// Controls the degree of parallelism and failure strategy when multiple event handlers
/// are registered for the same event type.
/// </remarks>
public sealed class ParallelEventHandlerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether parallel execution is enabled.
	/// </summary>
	/// <value><see langword="true"/> if enabled; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of handlers to execute concurrently.
	/// </summary>
	/// <value>The max degree of parallelism. Defaults to <see cref="Environment.ProcessorCount"/>.</value>
	[Range(1, 1024)]
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the failure handling strategy for parallel execution.
	/// </summary>
	/// <value>The strategy to use. Defaults to <see cref="WhenAllStrategy.WaitAll"/>.</value>
	public WhenAllStrategy WhenAllStrategy { get; set; } = WhenAllStrategy.WaitAll;

	/// <summary>
	/// Gets or sets the timeout for the entire parallel handler execution.
	/// </summary>
	/// <value>The timeout. <see langword="null"/> means no timeout. Defaults to <see langword="null"/>.</value>
	public TimeSpan? Timeout { get; set; }
}
