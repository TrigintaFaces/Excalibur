// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures protection against authentication attacks and abuse.
/// </summary>
public sealed class AuthenticationProtectionOptions
{
	/// <summary>
	/// Gets a value indicating whether authentication failure protection is enabled.
	/// </summary>
	/// <value> True to enable protection against authentication attacks, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the maximum consecutive authentication failures before lockout.
	/// </summary>
	/// <value> The number of consecutive failures to trigger protection measures. Defaults to 5. </value>
	[Range(1, 100)]
	public int MaxConsecutiveFailures { get; init; } = 5;

	/// <summary>
	/// Gets the lockout duration after exceeding failure threshold.
	/// </summary>
	/// <value> The time to block authentication attempts after lockout. Defaults to 15 minutes. </value>
	public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

	/// <summary>
	/// Gets the time window for failure rate calculation.
	/// </summary>
	/// <value> The time window to count failures for rate limiting. Defaults to 1 hour. </value>
	public TimeSpan FailureWindow { get; init; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets a value indicating whether to enable exponential backoff for repeated failures.
	/// </summary>
	/// <value> True to increase delays between retry attempts, false for fixed delays. </value>
	public bool UseExponentialBackoff { get; init; } = true;
}
