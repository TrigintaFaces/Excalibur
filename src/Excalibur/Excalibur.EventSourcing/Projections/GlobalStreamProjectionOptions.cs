// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>The name used when a host has not been given one explicitly.</summary>
	/// <remarks>
	/// Exposed as a constant so a host can tell "the consumer chose this" from "nobody chose", and derive
	/// a unique name in the second case. A shared default is not a safe fallback here: two hosts holding
	/// the same name share one checkpoint row, and the compare-and-set then does exactly what it was told
	/// on an identifier that names two different subscriptions.
	/// </remarks>
	public const string DefaultProjectionName = "AsyncProjectionProcessingHost";

	/// <summary>
	/// Gets or sets the name of the projection for checkpoint tracking.
	/// </summary>
	/// <remarks>
	/// A checkpoint is keyed by this name, so two hosts sharing it share one mark: the first to advance
	/// wins and the other is told it was superseded, on a subscription that was never actually contested.
	/// Hosts that can derive a unique name from their own type do so when this is left at the default;
	/// set it explicitly when running more than one host of the same type.
	/// </remarks>
	/// <value>The projection name. Defaults to <see cref="DefaultProjectionName"/>.</value>
	public string ProjectionName { get; set; } = DefaultProjectionName;

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
