// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

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
	/// Gets a value indicating whether to perform rotation during maintenance windows only.
	/// </summary>
	/// <value> True to restrict rotation to maintenance windows, false to allow any time. </value>
	public bool MaintenanceWindowOnly { get; init; } = true;

	/// <summary>
	/// Gets the maximum number of rotation retries on failure.
	/// </summary>
	/// <value> The number of retry attempts for failed rotations. Defaults to 3. </value>
	[Range(1, 10)]
	public int MaxRetries { get; init; } = 3;
}
