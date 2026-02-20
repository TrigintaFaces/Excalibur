// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Result of version compatibility checking.
/// </summary>
public sealed class VersionCompatibilityResult
{
	private VersionCompatibilityResult(VersionCompatibilityStatus status, string? reason)
	{
		Status = status;
		Reason = reason;
	}

	/// <summary>
	/// Gets the compatibility status.
	/// </summary>
	/// <value>The current <see cref="Status"/> value.</value>
	public VersionCompatibilityStatus Status { get; }

	/// <summary>
	/// Gets the reason for the compatibility status.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string? Reason { get; }

	/// <summary>
	/// Creates a compatible result.
	/// </summary>
	public static VersionCompatibilityResult Compatible() => new(VersionCompatibilityStatus.Compatible, reason: null);

	/// <summary>
	/// Creates a deprecated result.
	/// </summary>
	public static VersionCompatibilityResult Deprecated(string reason) =>
		new(VersionCompatibilityStatus.Deprecated, reason);

	/// <summary>
	/// Creates an incompatible result.
	/// </summary>
	public static VersionCompatibilityResult Incompatible(string reason) =>
		new(VersionCompatibilityStatus.Incompatible, reason);

	/// <summary>
	/// Creates an unknown status result.
	/// </summary>
	public static VersionCompatibilityResult Unknown(string reason) =>
		new(VersionCompatibilityStatus.Unknown, reason);
}
