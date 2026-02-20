// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines rotation policies for secrets and keys.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecretRotationPolicy" /> class.
/// </remarks>
/// <param name="enabled"> Whether automatic rotation is enabled. </param>
/// <param name="rotationInterval"> The interval between rotations. </param>
/// <param name="warningThreshold"> The time before rotation to issue warnings. </param>
/// <param name="maxRetries"> The maximum number of rotation retries. </param>
public sealed class SecretRotationPolicy(
	bool enabled = false,
	TimeSpan? rotationInterval = null,
	TimeSpan? warningThreshold = null,
	int maxRetries = 3)
{
	/// <summary>
	/// Gets a value indicating whether automatic rotation is enabled.
	/// </summary>
	/// <value> True if the secret should be automatically rotated, false otherwise. </value>
	public bool Enabled { get; } = enabled;

	/// <summary>
	/// Gets the interval between automatic rotations.
	/// </summary>
	/// <value> The time period between scheduled rotations. </value>
	public TimeSpan RotationInterval { get; } = rotationInterval ?? TimeSpan.FromDays(30);

	/// <summary>
	/// Gets the warning threshold before rotation.
	/// </summary>
	/// <value> The time before rotation to start issuing warnings. </value>
	public TimeSpan WarningThreshold { get; } = warningThreshold ?? TimeSpan.FromDays(7);

	/// <summary>
	/// Gets the maximum number of rotation retries on failure.
	/// </summary>
	/// <value> The number of times to retry a failed rotation. </value>
	public int MaxRetries { get; } = maxRetries;
}
