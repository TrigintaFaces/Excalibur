// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Configuration options for global stream projection processing.
/// </summary>
public sealed class GlobalStreamProjectionOptions
{
	/// <summary>
	/// Gets or sets the interval at which checkpoints are persisted.
	/// </summary>
	/// <value>The checkpoint interval in number of events processed. Default is 100.</value>
	[Range(1, 100000)]
	public int CheckpointInterval { get; set; } = 100;

	/// <summary>
	/// Gets or sets the name of the projection for checkpoint tracking.
	/// </summary>
	/// <value>The projection name. Default is "GlobalStreamProjection".</value>
	public string ProjectionName { get; set; } = "GlobalStreamProjection";

	/// <summary>
	/// Gets or sets the maximum number of events to read per batch.
	/// </summary>
	/// <value>The batch size. Default is 500.</value>
	[Range(1, 100000)]
	public int BatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the polling interval when no new events are available.
	/// </summary>
	/// <value>The idle polling interval. Default is 1 second.</value>
	public TimeSpan IdlePollingInterval { get; set; } = TimeSpan.FromSeconds(1);
}
