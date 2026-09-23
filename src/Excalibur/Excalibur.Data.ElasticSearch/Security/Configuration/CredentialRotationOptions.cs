// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures automatic credential rotation and lifecycle management.
/// </summary>
public sealed class CredentialRotationOptions
{
	/// <summary>
	/// Gets a value indicating whether automatic credential rotation is enabled.
	/// </summary>
	/// <value> True to enable automatic credential rotation, false for manual rotation only. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the rotation interval for credentials.
	/// </summary>
	/// <value> The time interval between automatic credential rotations. Defaults to 30 days. </value>
	public TimeSpan RotationInterval { get; init; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets the rotation warning threshold.
	/// </summary>
	/// <value> The time before rotation to issue warnings. Defaults to 7 days. </value>
	public TimeSpan WarningThreshold { get; init; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets the number of times a failed scheduled rotation is retried before it is abandoned until the
	/// next interval.
	/// </summary>
	/// <value> The retry count. Defaults to 3. Zero makes a single attempt and does not retry. </value>
	/// <remarks>
	/// A scheduled rotation that makes one attempt leaves credentials un-rotated for a whole
	/// <see cref="RotationInterval"/> -- up to thirty days by default -- because one request happened to
	/// fail. The retries are attempted within the interval, so a transient failure costs seconds rather
	/// than a rotation period.
	/// </remarks>
	public int MaxRetries { get; init; } = 3;

	/// <summary>
	/// Gets the base delay before the first retry of a failed scheduled rotation.
	/// </summary>
	/// <value> The base delay. Defaults to 30 seconds. Backoff is exponential with jitter. </value>
	public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(30);
}
