// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Default implementation of <see cref="IFipsDetector"/> that checks the operating system
/// for FIPS 140-2 compliance mode.
/// </summary>
/// <remarks>
/// <para>
/// FIPS 140-2 status is determined by:
/// <list type="bullet">
/// <item>Windows: CryptoConfig.AllowOnlyFipsAlgorithms (registry setting)</item>
/// <item>Linux: /proc/sys/crypto/fips_enabled kernel parameter</item>
/// <item>macOS: Not applicable (uses Common Criteria certification)</item>
/// </list>
/// </para>
/// <para>
/// This implementation caches the FIPS status on first access since it cannot change
/// without a system restart.
/// </para>
/// </remarks>
public sealed partial class DefaultFipsDetector : IFipsDetector
{
	private readonly ILogger<DefaultFipsDetector> _logger;
	private readonly Lazy<FipsDetectionResult> _cachedResult;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultFipsDetector"/> class.
	/// </summary>
	/// <param name="logger">The logger for diagnostics.</param>
	public DefaultFipsDetector(ILogger<DefaultFipsDetector> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cachedResult = new Lazy<FipsDetectionResult>(DetectFipsStatus);
	}

	/// <inheritdoc/>
	public bool IsFipsEnabled => _cachedResult.Value.IsFipsEnabled;

	/// <inheritdoc/>
	public FipsDetectionResult GetStatus() => _cachedResult.Value;

	[LoggerMessage(LogLevel.Warning, "Error detecting FIPS compliance status")]
	private partial void LogErrorDetectingStatus(Exception ex);

	[LoggerMessage(LogLevel.Information, "Windows FIPS mode: {IsFipsEnabled}")]
	private partial void LogWindowsFipsMode(bool isFipsEnabled);

	[LoggerMessage(LogLevel.Information, "Linux FIPS mode: {IsFipsEnabled} (value: {Value})")]
	private partial void LogLinuxFipsMode(bool isFipsEnabled, string value);

	[LoggerMessage(LogLevel.Warning, "Error reading Linux FIPS status")]
	private partial void LogErrorReadingLinuxStatus(Exception ex);

	private FipsDetectionResult DetectFipsStatus()
	{
		try
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return DetectWindowsFipsStatus();
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return DetectLinuxFipsStatus();
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// macOS uses Common Criteria certification, not FIPS.
				return FipsDetectionResult.Disabled(
					"macOS",
					"macOS uses Common Criteria certification. FIPS mode not applicable.");
			}

			return FipsDetectionResult.Disabled(
				"Unknown",
				"Unable to determine FIPS status for this platform.");
		}
		catch (Exception ex)
		{
			LogErrorDetectingStatus(ex);
			return FipsDetectionResult.Disabled(
				RuntimeInformation.OSDescription,
				$"Error detecting FIPS status: {ex.Message}");
		}
	}

	private FipsDetectionResult DetectWindowsFipsStatus()
	{
		// On Windows, CryptoConfig.AllowOnlyFipsAlgorithms reflects the registry setting
		// HKLM\SYSTEM\CurrentControlSet\Control\Lsa\FipsAlgorithmPolicy\Enabled.
		var isFipsEnabled = CryptoConfig.AllowOnlyFipsAlgorithms;

		LogWindowsFipsMode(isFipsEnabled);

		return isFipsEnabled
			? FipsDetectionResult.Enabled(
				"Windows",
				"Windows FIPS mode enabled (FipsAlgorithmPolicy registry key)")
			: FipsDetectionResult.Disabled(
				"Windows",
				"Windows FIPS mode disabled. Enable via Local Security Policy or registry.");
	}

	private FipsDetectionResult DetectLinuxFipsStatus()
	{
		// On Linux, check /proc/sys/crypto/fips_enabled.
		const string fipsFile = "/proc/sys/crypto/fips_enabled";

		try
		{
			if (File.Exists(fipsFile))
			{
				var content = File.ReadAllText(fipsFile).Trim();
				var isFipsEnabled = content == "1";

				LogLinuxFipsMode(isFipsEnabled, content);

				return isFipsEnabled
					? FipsDetectionResult.Enabled(
						"Linux",
						"Linux FIPS mode enabled (kernel parameter)")
					: FipsDetectionResult.Disabled(
						"Linux",
						"Linux FIPS mode disabled. Enable via kernel boot parameter fips=1.");
			}

			// Also check .NET's CryptoConfig as fallback.
			var dotnetFips = CryptoConfig.AllowOnlyFipsAlgorithms;

			return dotnetFips
				? FipsDetectionResult.Enabled(
					"Linux",
					".NET FIPS-only mode enabled")
				: FipsDetectionResult.Disabled(
					"Linux",
					"FIPS status file not found. FIPS mode likely not enabled.");
		}
		catch (Exception ex)
		{
			LogErrorReadingLinuxStatus(ex);
			return FipsDetectionResult.Disabled(
				"Linux",
				$"Error checking FIPS status: {ex.Message}");
		}
	}
}
