// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Configuration options for global stream projection processing.
/// </summary>
public sealed class GlobalStreamProjectionOptions
{
	/// <summary>Gets or sets the event interval between checkpoint writes for custom global-stream hosts.</summary>
	/// <value>The interval for GlobalStreamProjectionHost implementations; default is 100.</value>
	/// <remarks>
	/// The built-in asynchronous projection processor ignores this interval and checkpoints every finite
	/// page after all required folds settle. A crash can redeliver that page, up to BatchSize events per
	/// attempt. Repeated failures can repeat effects again; there is no bound on the total number of
	/// attempts. Custom global-stream hosts using this threshold can overshoot it by a page and have a
	/// different replay window. External handler effects must be idempotent in either case because their
	/// commit and the checkpoint commit are separate operations.
	/// </remarks>
	[Range(1, 100000)]
	public int CheckpointInterval { get; set; } = 100;

	/// <summary>The default logical subscription name.</summary>
	/// <remarks>Cooperating built-in processors may share it only with an identical bound definition. Choose an explicit stable name for each independent subscription.</remarks>
	public const string DefaultProjectionName = "AsyncProjectionProcessingHost";

	/// <summary>Gets or sets the stable subscription name used for checkpoint and definition binding.</summary>
	/// <remarks>
	/// API and Jobs processes processing the same work use the same name, scope, revision and projection
	/// set. A different definition under this name is refused before effects. To deploy changed projection
	/// semantics, choose a new subscription name and deliberately rebuild or cut over its destination.
	/// </remarks>
	/// <value>Defaults to DefaultProjectionName.</value>
	public string ProjectionName { get; set; } = DefaultProjectionName;

	/// <summary>Gets or sets the stable logical identity of the event source and projection destination.</summary>
	/// <remarks>All cooperating processes must use the same value. Required for asynchronous projection processing.</remarks>
	public string SubscriptionScope { get; set; } = string.Empty;

	/// <summary>Gets or sets the consumer's projection-definition revision.</summary>
	/// <remarks>Change this when fold logic, routing, dependencies, or destination semantics change; use a new subscription and rebuild.</remarks>
	public string SubscriptionRevision { get; set; } = "1";


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
