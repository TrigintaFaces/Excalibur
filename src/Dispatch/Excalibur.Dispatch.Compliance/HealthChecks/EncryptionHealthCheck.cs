// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// ASP.NET Core health check for encryption infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the encryption infrastructure is properly configured
/// and functioning. It performs:
/// </para>
/// <list type="bullet">
///   <item>Provider availability check</item>
///   <item>Key management connectivity check</item>
///   <item>Round-trip encryption verification</item>
///   <item>Performance measurement (optional degraded state on slow operations)</item>
/// </list>
/// <para>
/// <b>Health Check Results:</b>
/// </para>
/// <list type="bullet">
///   <item><b>Healthy:</b> All encryption operations pass verification</item>
///   <item><b>Degraded:</b> Some operations are slow or non-critical failures</item>
///   <item><b>Unhealthy:</b> Critical encryption failure</item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;EncryptionHealthCheck&gt;("encryption");
/// </code>
/// </remarks>
public sealed partial class EncryptionHealthCheck : IHealthCheck
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IKeyManagementProvider? _keyManagementProvider;
	private readonly IEncryptionTelemetry? _telemetry;
	private readonly ILogger<EncryptionHealthCheck> _logger;
	private readonly EncryptionHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionHealthCheck"/> class.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider to verify.</param>
	/// <param name="keyManagementProvider">Optional key management provider.</param>
	/// <param name="telemetry">Optional telemetry for reporting health status.</param>
	/// <param name="logger">The logger instance.</param>
	public EncryptionHealthCheck(
		IEncryptionProvider encryptionProvider,
		IKeyManagementProvider? keyManagementProvider,
		IEncryptionTelemetry? telemetry,
		ILogger<EncryptionHealthCheck> logger)
		: this(encryptionProvider, keyManagementProvider, telemetry, logger, EncryptionHealthCheckOptions.Default)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionHealthCheck"/> class with options.
	/// </summary>
	/// <param name="encryptionProvider">The encryption provider to verify.</param>
	/// <param name="keyManagementProvider">Optional key management provider.</param>
	/// <param name="telemetry">Optional telemetry for reporting health status.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">Health check options.</param>
	public EncryptionHealthCheck(
		IEncryptionProvider encryptionProvider,
		IKeyManagementProvider? keyManagementProvider,
		IEncryptionTelemetry? telemetry,
		ILogger<EncryptionHealthCheck> logger,
		EncryptionHealthCheckOptions options)
	{
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_keyManagementProvider = keyManagementProvider;
		_telemetry = telemetry;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	private enum VerificationStatus
	{
		Passed,
		Slow,
		Failed,
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>();
		var failures = new List<string>();
		var warnings = new List<string>();

		// Check 1: Provider information
		data["provider_name"] = _encryptionProvider.GetType().Name;

		// Check 2: Verify round-trip encryption
		var roundTripResult = await VerifyRoundTripAsync(cancellationToken).ConfigureAwait(false);
		data["round_trip_result"] = roundTripResult.ToString();
		data["round_trip_duration_ms"] = roundTripResult.DurationMs;

		if (roundTripResult.Status == VerificationStatus.Failed)
		{
			failures.Add($"Round-trip verification failed: {roundTripResult.ErrorMessage}");
		}
		else if (roundTripResult.Status == VerificationStatus.Slow)
		{
			warnings.Add($"Round-trip is slow ({roundTripResult.DurationMs:F2}ms > {_options.DegradedThreshold.TotalMilliseconds}ms)");
		}

		// Check 3: Verify key management if available
		if (_keyManagementProvider is not null && _options.VerifyKeyManagement)
		{
			var keyResult = await VerifyKeyManagementAsync(cancellationToken).ConfigureAwait(false);
			data["key_management_result"] = keyResult.ToString();

			if (keyResult.Status == VerificationStatus.Failed)
			{
				failures.Add($"Key management check failed: {keyResult.ErrorMessage}");
			}
			else if (keyResult.Status == VerificationStatus.Slow)
			{
				warnings.Add($"Key management is slow ({keyResult.DurationMs:F2}ms)");
			}

			if (keyResult.ActiveKeyId is not null)
			{
				data["active_key_id"] = keyResult.ActiveKeyId;
			}
		}

		// Report health via telemetry
		if (_telemetry?.GetService(typeof(IEncryptionTelemetryDetails)) is IEncryptionTelemetryDetails telemetryDetails)
		{
			var healthStatus = failures.Count > 0 ? "unhealthy" : (warnings.Count > 0 ? "degraded" : "healthy");
			var healthScore = failures.Count > 0 ? 0 : (warnings.Count > 0 ? 50 : 100);
			telemetryDetails.UpdateProviderHealth(_encryptionProvider.GetType().Name, healthStatus, healthScore);
		}

		// Determine overall health status
		if (failures.Count > 0)
		{
			var description = $"Encryption health check failed: {string.Join("; ", failures)}";
			LogEncryptionHealthCheckFailed(string.Join("; ", failures));

			return HealthCheckResult.Unhealthy(
				description: description,
				data: data);
		}

		if (warnings.Count > 0)
		{
			var description = $"Encryption health check degraded: {string.Join("; ", warnings)}";
			LogEncryptionHealthCheckDegraded(string.Join("; ", warnings));

			return HealthCheckResult.Degraded(
				description: description,
				data: data);
		}

		LogEncryptionHealthCheckPassed();

		return HealthCheckResult.Healthy(
			description: "Encryption provider is healthy",
			data: data);
	}

	private async Task<VerificationResult> VerifyRoundTripAsync(CancellationToken cancellationToken)
	{
		var testData = new byte[32];
		RandomNumberGenerator.Fill(testData);

		var context = new EncryptionContext { Purpose = "health-check", };

		var stopwatch = Stopwatch.StartNew();
		try
		{
			// Encrypt
			var encrypted = await _encryptionProvider.EncryptAsync(testData, context, cancellationToken)
				.ConfigureAwait(false);

			// Decrypt
			var decrypted = await _encryptionProvider.DecryptAsync(encrypted, context, cancellationToken)
				.ConfigureAwait(false);

			stopwatch.Stop();
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;

			// Verify integrity
			if (!decrypted.SequenceEqual(testData))
			{
				return new VerificationResult(
					VerificationStatus.Failed,
					durationMs,
					"Round-trip data mismatch");
			}

			// Check for slow operation
			if (stopwatch.Elapsed > _options.DegradedThreshold)
			{
				return new VerificationResult(
					VerificationStatus.Slow,
					durationMs,
					null);
			}

			return new VerificationResult(
				VerificationStatus.Passed,
				durationMs,
				null);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			LogEncryptionHealthCheckRoundTripFailed(ex);

			return new VerificationResult(
				VerificationStatus.Failed,
				stopwatch.Elapsed.TotalMilliseconds,
				ex.Message);
		}
	}

	private async Task<KeyManagementResult> VerifyKeyManagementAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
			var activeKey = await _keyManagementProvider.GetActiveKeyAsync(
					purpose: null,
					cancellationToken)
				.ConfigureAwait(false);

			stopwatch.Stop();
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;

			if (activeKey is null)
			{
				return new KeyManagementResult(
					VerificationStatus.Failed,
					durationMs,
					"No active key available",
					null);
			}

			// Check for slow operation
			if (stopwatch.Elapsed > _options.DegradedThreshold)
			{
				return new KeyManagementResult(
					VerificationStatus.Slow,
					durationMs,
					null,
					activeKey.KeyId);
			}

			return new KeyManagementResult(
				VerificationStatus.Passed,
				durationMs,
				null,
				activeKey.KeyId);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			LogEncryptionHealthCheckKeyManagementFailed(ex);

			return new KeyManagementResult(
				VerificationStatus.Failed,
				stopwatch.Elapsed.TotalMilliseconds,
				ex.Message,
				null);
		}
	}

	[LoggerMessage(
		ComplianceEventId.EncryptionHealthCheckFailed,
		LogLevel.Error,
		"Encryption health check failed: {Failures}")]
	private partial void LogEncryptionHealthCheckFailed(string failures);

	[LoggerMessage(
		ComplianceEventId.EncryptionHealthCheckDegraded,
		LogLevel.Warning,
		"Encryption health check degraded: {Warnings}")]
	private partial void LogEncryptionHealthCheckDegraded(string warnings);

	[LoggerMessage(
		ComplianceEventId.EncryptionHealthCheckPassed,
		LogLevel.Debug,
		"Encryption health check passed")]
	private partial void LogEncryptionHealthCheckPassed();

	[LoggerMessage(
		ComplianceEventId.EncryptionHealthCheckRoundTripFailed,
		LogLevel.Warning,
		"Encryption round-trip verification failed")]
	private partial void LogEncryptionHealthCheckRoundTripFailed(Exception exception);

	[LoggerMessage(
		ComplianceEventId.EncryptionHealthCheckKeyManagementFailed,
		LogLevel.Warning,
		"Key management verification failed")]
	private partial void LogEncryptionHealthCheckKeyManagementFailed(Exception exception);

	private readonly record struct VerificationResult(
		VerificationStatus Status,
		double DurationMs,
		string? ErrorMessage)
	{
		public override string ToString() => Status switch
		{
			VerificationStatus.Passed => $"Passed ({DurationMs:F2}ms)",
			VerificationStatus.Slow => $"Slow ({DurationMs:F2}ms)",
			VerificationStatus.Failed => $"Failed: {ErrorMessage}",
			_ => "Unknown",
		};
	}

	private readonly record struct KeyManagementResult(
		VerificationStatus Status,
		double DurationMs,
		string? ErrorMessage,
		string? ActiveKeyId)
	{
		public override string ToString() => Status switch
		{
			VerificationStatus.Passed => $"Passed ({DurationMs:F2}ms) - Key: {ActiveKeyId}",
			VerificationStatus.Slow => $"Slow ({DurationMs:F2}ms) - Key: {ActiveKeyId}",
			VerificationStatus.Failed => $"Failed: {ErrorMessage}",
			_ => "Unknown",
		};
	}
}

/// <summary>
/// Options for configuring encryption health checks.
/// </summary>
public sealed record EncryptionHealthCheckOptions
{
	/// <summary>
	/// Gets the threshold for marking the health check as degraded.
	/// </summary>
	/// <value>Default is 100 milliseconds.</value>
	public TimeSpan DegradedThreshold { get; init; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets a value indicating whether to verify key management.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool VerifyKeyManagement { get; init; } = true;

	/// <summary>
	/// Gets the default options.
	/// </summary>
	public static EncryptionHealthCheckOptions Default => new();
}
