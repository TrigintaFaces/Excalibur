// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides detection of FIPS 140-2 compliance mode on the current system.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables unit testing of FIPS-dependent code by allowing
/// the FIPS detection mechanism to be mocked. Production implementations
/// should check the actual operating system FIPS mode setting.
/// </para>
/// <para>
/// FIPS 140-2 compliance is required for regulated environments.
/// This interface allows encryption providers to verify FIPS mode without
/// coupling to platform-specific detection logic.
/// </para>
/// </remarks>
public interface IFipsDetector
{
	/// <summary>
	/// Gets a value indicating whether the system is running in FIPS 140-2 mode.
	/// </summary>
	bool IsFipsEnabled { get; }

	/// <summary>
	/// Gets the detailed FIPS compliance status of the current system.
	/// </summary>
	/// <returns>A status object containing platform and validation details.</returns>
	FipsDetectionResult GetStatus();
}

/// <summary>
/// Represents the result of FIPS 140-2 detection on the current system.
/// </summary>
public sealed record FipsDetectionResult
{
	/// <summary>
	/// Gets a value indicating whether FIPS 140-2 mode is enabled.
	/// </summary>
	public required bool IsFipsEnabled { get; init; }

	/// <summary>
	/// Gets the platform identifier (e.g., "Windows", "Linux", "macOS").
	/// </summary>
	public required string Platform { get; init; }

	/// <summary>
	/// Gets details about how FIPS status was validated.
	/// </summary>
	public required string ValidationDetails { get; init; }

	/// <summary>
	/// Gets the timestamp when FIPS status was detected.
	/// </summary>
	public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a result indicating FIPS is enabled.
	/// </summary>
	/// <param name="platform">The platform identifier.</param>
	/// <param name="details">Validation details.</param>
	/// <returns>A FIPS-enabled result.</returns>
	public static FipsDetectionResult Enabled(string platform, string details) =>
		new() { IsFipsEnabled = true, Platform = platform, ValidationDetails = details };

	/// <summary>
	/// Creates a result indicating FIPS is disabled.
	/// </summary>
	/// <param name="platform">The platform identifier.</param>
	/// <param name="details">Validation details.</param>
	/// <returns>A FIPS-disabled result.</returns>
	public static FipsDetectionResult Disabled(string platform, string details) =>
		new() { IsFipsEnabled = false, Platform = platform, ValidationDetails = details };
}
