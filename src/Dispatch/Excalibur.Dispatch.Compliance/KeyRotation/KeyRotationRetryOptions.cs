// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Retry configuration for key rotation operations.
/// </summary>
public sealed class KeyRotationRetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to retry failed rotations.
	/// </summary>
	public bool RetryFailedRotations { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed rotations.
	/// </summary>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);
}
