// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Configuration options for projection rebuild operations.
/// </summary>
public sealed class ProjectionRebuildOptions
{
    /// <summary>Gets or sets explicit acknowledgement that this in-place rebuild runs offline and exclusively.</summary>
    /// <value>False by default. Set true only after stopping and draining every writer and other rebuild process for the entire operation.</value>
    /// <remarks>This is an operator prerequisite, not a distributed lock. For online changes, populate a separate versioned destination and switch readers through the generation cutover gate.</remarks>
    public bool OfflineRebuildAcknowledged { get; set; }

	/// <summary>
	/// Gets or sets the number of events to process per batch during rebuild.
	/// </summary>
	/// <value>The batch size. Default is 500.</value>
	[Range(1, 100000)]
	public int BatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the delay between batches to reduce load.
	/// </summary>
	/// <value>The batch delay. Default is 10 milliseconds.</value>
	public TimeSpan BatchDelay { get; set; } = TimeSpan.FromMilliseconds(10);

	/// <summary>
	/// Gets or sets a value indicating whether to rebuild on application startup.
	/// </summary>
	/// <value><see langword="true"/> to rebuild on startup; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool RebuildOnStartup { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of concurrent rebuilds in this service instance.
	/// </summary>
	/// <value>The parallelism degree. Default is 1 (sequential).</value>
	/// <remarks>Queued calls honor cancellation. This process-local limit does not coordinate other hosts or limit continuous projection processing. Size the aggregate host limits to leave database capacity for live traffic.</remarks>
	[Range(1, 32)]
	public int MaxParallelism { get; set; } = 1;
}
