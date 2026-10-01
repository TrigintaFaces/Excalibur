// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0



using System.ComponentModel.DataAnnotations;

namespace Excalibur.Compliance;

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
	[Required]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets the key purpose this policy applies to. Null applies to all purposes without a specific policy.
	/// </summary>
	public string? Purpose { get; set; }

	/// <summary>
	/// Gets the maximum age of a key before rotation is required.
	/// </summary>
	/// <remarks>
	/// Default is 90 days per SOC 2 and PCI DSS recommendations.
	/// NIST 800-57 recommends rotation at least annually.
	/// </remarks>
	public TimeSpan MaxKeyAge { get; set; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets the encryption algorithm to use when creating new key versions.
	/// </summary>
	public EncryptionAlgorithm Algorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets a value indicating whether automatic rotation is enabled for this policy.
	/// </summary>
	public bool AutoRotateEnabled { get; set; } = true;

	/// <summary>
	/// Gets the number of days before rotation to generate a warning.
	/// </summary>
	public int WarningDaysBeforeRotation { get; set; } = 14;

	/// <summary>
	/// Gets a value indicating whether to send notifications before rotation.
	/// </summary>
	public bool NotifyBeforeRotation { get; set; } = true;

	/// <summary>
	/// Gets the number of previous key versions to retain for decryption.
	/// </summary>
	/// <remarks>
	/// This supports zero-downtime rotation by allowing decryption with
	/// previous key versions while new encryptions use the latest version.
	/// </remarks>
	public int RetainedVersionCount { get; set; } = 3;

	/// <summary>
	/// Gets a value indicating whether to require FIPS 140-2 compliant key generation.
	/// </summary>
	public bool RequireFipsCompliance { get; set; }

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

		// A KEY OF UNKNOWN AGE IS DUE. Neither the last rotation nor the creation instant is known, so
		// nothing can show this key is still within its maximum age -- and the only safe reading of "cannot be
		// shown to be young enough" is "rotate it". Rotating a key that did not need it costs one rotation;
		// leaving a key that did costs the property the maximum age exists to provide.
		//
		// Stated explicitly rather than left to the lifted comparison, which resolves the other way: a null
		// TimeSpan compared with >= yields false, so an unknown-age key would report NOT due and the absence
		// would silently pick the unsafe direction.
		var lastRotation = key.LastRotatedAt ?? key.CreatedAt;
		if (lastRotation is null)
		{
			return true;
		}

		var timeSinceRotation = DateTimeOffset.UtcNow - lastRotation.Value;

		return timeSinceRotation >= MaxKeyAge;
	}

	/// <summary>
	/// Gets the next scheduled rotation time for a key.
	/// </summary>
	/// <param name="key">The key metadata.</param>
	/// <returns>The next rotation time based on this policy.</returns>
	public DateTimeOffset GetNextRotationTime(KeyMetadata key)
	{
		// Already overdue when the age is unknown, for the reason above. MinValue rather than the current
		// instant because it is overdue under every comparison a caller might make and reads no clock -- and
		// because a date that is obviously not a real schedule is the loud answer, where a plausible "now"
		// would look like a computed one.
		var lastRotation = key.LastRotatedAt ?? key.CreatedAt;

		return lastRotation is null ? DateTimeOffset.MinValue : lastRotation.Value.Add(MaxKeyAge);
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
