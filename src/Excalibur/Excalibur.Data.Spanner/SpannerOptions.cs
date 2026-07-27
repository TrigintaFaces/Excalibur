// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Spanner;

/// <summary>
/// Configuration for the Google Cloud Spanner data provider foundation shared by the
/// <c>Excalibur.*.Spanner</c> persistence stores.
/// </summary>
/// <remarks>
/// Spanner is addressed by a three-part database path (<c>projects/{project}/instances/{instance}/databases/{database}</c>).
/// Set <see cref="EmulatorHost"/> (or the <c>SPANNER_EMULATOR_HOST</c> environment variable) to target the
/// official Spanner emulator for local development and integration tests.
/// </remarks>
public sealed class SpannerOptions
{
	/// <summary>Gets or sets the Google Cloud project id that owns the Spanner instance.</summary>
	/// <value>The project id; required.</value>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>Gets or sets the Spanner instance id.</summary>
	/// <value>The instance id; required.</value>
	public string InstanceId { get; set; } = string.Empty;

	/// <summary>Gets or sets the Spanner database id.</summary>
	/// <value>The database id; required.</value>
	public string DatabaseId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the emulator host (<c>host:port</c>) to target instead of the production Spanner service.
	/// </summary>
	/// <value>The emulator endpoint, or <see langword="null"/> to use the production service. When set, this
	/// takes precedence over the <c>SPANNER_EMULATOR_HOST</c> environment variable.</value>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of times an <c>ABORTED</c> transaction is retried before failing.
	/// </summary>
	/// <value>The retry count; must be non-negative. Defaults to 5. Spanner uses optimistic concurrency and
	/// surfaces write-write conflicts as <c>ABORTED</c>; the provider replays the transaction with backoff.</value>
	public int MaxAbortRetries { get; set; } = 5;

	/// <summary>Gets or sets the base backoff applied between <c>ABORTED</c> retries, in milliseconds.</summary>
	/// <value>The base backoff in milliseconds; must be non-negative. Defaults to 25. The effective delay grows
	/// exponentially with the attempt number.</value>
	public int AbortRetryBaseDelayMilliseconds { get; set; } = 25;

	/// <summary>
	/// Gets the fully-qualified Spanner database path (<c>projects/{project}/instances/{instance}/databases/{database}</c>).
	/// </summary>
	/// <value>The three-part database resource path derived from <see cref="ProjectId"/>, <see cref="InstanceId"/>,
	/// and <see cref="DatabaseId"/>.</value>
	public string DatabasePath => $"projects/{ProjectId}/instances/{InstanceId}/databases/{DatabaseId}";
}
