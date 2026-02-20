// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines a rotation policy for encryption keys.
/// </summary>
/// <remarks>
/// <para>
/// Key rotation policies determine when keys should be rotated.
/// Different key purposes may have different rotation schedules based on
/// compliance requirements and risk assessment.
/// </para>
/// </remarks>
public sealed record KeyRotationPolicy
{
	/// <summary>
	/// Gets the name of this policy.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the key purpose this policy applies to. Null applies to all purposes without a specific policy.
	/// </summary>
	public string? Purpose { get; init; }

	/// <summary>
	/// Gets the maximum age of a key before rotation is required.
	/// </summary>
	/// <remarks>
	/// Default is 90 days per SOC 2 and PCI DSS recommendations.
	/// NIST 800-57 recommends rotation at least annually.
	/// </remarks>
	public TimeSpan MaxKeyAge { get; init; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets the encryption algorithm to use when creating new key versions.
	/// </summary>
	public EncryptionAlgorithm Algorithm { get; init; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets a value indicating whether automatic rotation is enabled for this policy.
	/// </summary>
	public bool AutoRotateEnabled { get; init; } = true;

	/// <summary>
	/// Gets the number of days before rotation to generate a warning.
	/// </summary>
	public int WarningDaysBeforeRotation { get; init; } = 14;

	/// <summary>
	/// Gets a value indicating whether to send notifications before rotation.
	/// </summary>
	public bool NotifyBeforeRotation { get; init; } = true;

	/// <summary>
	/// Gets the number of previous key versions to retain for decryption.
	/// </summary>
	/// <remarks>
	/// This supports zero-downtime rotation by allowing decryption with
	/// previous key versions while new encryptions use the latest version.
	/// </remarks>
	public int RetainedVersionCount { get; init; } = 3;

	/// <summary>
	/// Gets a value indicating whether to require FIPS 140-2 compliant key generation.
	/// </summary>
	public bool RequireFipsCompliance { get; init; }

	/// <summary>
	/// Creates a default policy with 90-day rotation.
	/// </summary>
	public static KeyRotationPolicy Default => new()
	{
		Name = "Default",
		MaxKeyAge = TimeSpan.FromDays(90),
		AutoRotateEnabled = true
	};

	/// <summary>
	/// Creates a strict policy for high-security keys with 30-day rotation.
	/// </summary>
	public static KeyRotationPolicy HighSecurity => new()
	{
		Name = "HighSecurity",
		MaxKeyAge = TimeSpan.FromDays(30),
		AutoRotateEnabled = true,
		RequireFipsCompliance = true,
		WarningDaysBeforeRotation = 7,
		RetainedVersionCount = 5
	};

	/// <summary>
	/// Creates a policy for archival keys with annual rotation.
	/// </summary>
	public static KeyRotationPolicy Archival => new()
	{
		Name = "Archival",
		MaxKeyAge = TimeSpan.FromDays(365),
		AutoRotateEnabled = true,
		WarningDaysBeforeRotation = 30,
		RetainedVersionCount = 10
	};

	/// <summary>
	/// Determines whether the specified key metadata indicates rotation is due.
	/// </summary>
	/// <param name="key">The key metadata to check.</param>
	/// <returns>True if the key should be rotated; otherwise false.</returns>
	public bool IsRotationDue(KeyMetadata key)
	{
		if (!AutoRotateEnabled)
		{
			return false;
		}

		if (key.Status != KeyStatus.Active)
		{
			return false;
		}

		// Check if key has exceeded max age since last rotation
		var lastRotation = key.LastRotatedAt ?? key.CreatedAt;
		var timeSinceRotation = DateTimeOffset.UtcNow - lastRotation;

		return timeSinceRotation >= MaxKeyAge;
	}

	/// <summary>
	/// Gets the next scheduled rotation time for a key.
	/// </summary>
	/// <param name="key">The key metadata.</param>
	/// <returns>The next rotation time based on this policy.</returns>
	public DateTimeOffset GetNextRotationTime(KeyMetadata key)
	{
		var lastRotation = key.LastRotatedAt ?? key.CreatedAt;
		return lastRotation.Add(MaxKeyAge);
	}

	/// <summary>
	/// Determines whether a rotation warning should be generated for the key.
	/// </summary>
	/// <param name="key">The key metadata to check.</param>
	/// <returns>True if a warning should be generated; otherwise false.</returns>
	public bool ShouldWarn(KeyMetadata key)
	{
		if (!NotifyBeforeRotation || key.Status != KeyStatus.Active)
		{
			return false;
		}

		var nextRotation = GetNextRotationTime(key);
		var timeUntilRotation = nextRotation - DateTimeOffset.UtcNow;

		return timeUntilRotation <= TimeSpan.FromDays(WarningDaysBeforeRotation)
			&& timeUntilRotation > TimeSpan.Zero;
	}
}
