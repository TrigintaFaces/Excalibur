// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for the key rotation background service.
/// </summary>
public sealed class KeyRotationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the key rotation service is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between rotation checks.
	/// </summary>
	/// <remarks>
	/// Default is 1 hour. More frequent checks ensure timely rotation
	/// but increase operational overhead.
	/// </remarks>
	public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the default rotation policy applied when no specific policy matches.
	/// </summary>
	public KeyRotationPolicy DefaultPolicy { get; set; } = KeyRotationPolicy.Default;

	/// <summary>
	/// Gets the rotation policies for specific key purposes.
	/// </summary>
	public Dictionary<string, KeyRotationPolicy> PoliciesByPurpose { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets a value indicating whether to continue processing other keys if one fails.
	/// </summary>
	public bool ContinueOnError { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of concurrent rotation operations.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxConcurrentRotations { get; set; } = 4;

	/// <summary>
	/// Gets or sets a value indicating whether to record metrics for rotation operations.
	/// </summary>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the retry options for failed rotation operations.
	/// </summary>
	public KeyRotationRetryOptions Retry { get; set; } = new();

	/// <summary>
	/// Gets or sets the distributed lock options for rotation operations.
	/// </summary>
	public KeyRotationLockOptions Lock { get; set; } = new();

	/// <summary>
	/// Gets the rotation policy for a key with the specified purpose.
	/// </summary>
	/// <param name="purpose">The key purpose, or null for the default policy.</param>
	/// <returns>The matching policy or the default policy.</returns>
	public KeyRotationPolicy GetPolicyForPurpose(string? purpose)
	{
		if (purpose is not null && PoliciesByPurpose.TryGetValue(purpose, out var policy))
		{
			return policy;
		}

		return DefaultPolicy;
	}

	/// <summary>
	/// Adds a rotation policy for a specific key purpose.
	/// </summary>
	/// <param name="purpose">The key purpose.</param>
	/// <param name="policy">The rotation policy.</param>
	/// <returns>This options instance for chaining.</returns>
	public KeyRotationOptions AddPolicy(string purpose, KeyRotationPolicy policy)
	{
		PoliciesByPurpose[purpose] = policy;
		return this;
	}

	/// <summary>
	/// Adds a high-security policy for the specified purpose.
	/// </summary>
	/// <param name="purpose">The key purpose.</param>
	/// <returns>This options instance for chaining.</returns>
	public KeyRotationOptions AddHighSecurityPolicy(string purpose)
	{
		return AddPolicy(purpose, KeyRotationPolicy.HighSecurity with { Purpose = purpose });
	}

	/// <summary>
	/// Adds an archival policy for the specified purpose.
	/// </summary>
	/// <param name="purpose">The key purpose.</param>
	/// <returns>This options instance for chaining.</returns>
	public KeyRotationOptions AddArchivalPolicy(string purpose)
	{
		return AddPolicy(purpose, KeyRotationPolicy.Archival with { Purpose = purpose });
	}
}
