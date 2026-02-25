// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Configuration options for projection rebuild operations.
/// </summary>
public sealed class ProjectionRebuildOptions
{
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
	/// Gets or sets the maximum degree of parallelism for rebuilding multiple projections.
	/// </summary>
	/// <value>The parallelism degree. Default is 1 (sequential).</value>
	[Range(1, 32)]
	public int MaxParallelism { get; set; } = 1;
}
