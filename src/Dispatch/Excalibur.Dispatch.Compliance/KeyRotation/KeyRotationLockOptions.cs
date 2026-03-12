// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Distributed lock configuration for key rotation operations.
/// </summary>
public sealed class KeyRotationLockOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to skip keys that are already being rotated by another instance.
	/// </summary>
	/// <remarks>
	/// This is important for distributed deployments where multiple instances
	/// may be running the rotation service.
	/// </remarks>
	public bool SkipLockedKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration to hold a rotation lock on a key.
	/// </summary>
	public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the timeout for individual rotation operations.
	/// </summary>
	public TimeSpan RotationTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
