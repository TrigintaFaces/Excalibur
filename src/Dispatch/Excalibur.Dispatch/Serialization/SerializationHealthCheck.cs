// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// ASP.NET Core health check for serialization infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the serialization infrastructure is properly configured
/// and functioning. It performs:
/// </para>
/// <list type="bullet">
/// <item>Registry configuration check (current serializer set)</item>
/// <item>Round-trip verification for all registered serializers</item>
/// <item>Performance measurement (optional degraded state on slow serialization)</item>
/// </list>
/// <para>
/// <b>Health Check Results:</b>
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> All serializers pass round-trip verification</item>
/// <item><b>Degraded:</b> Some serializers failed or are slow</item>
/// <item><b>Unhealthy:</b> No current serializer configured or critical failure</item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// services.AddHealthChecks()
///     .AddCheck&lt;SerializationHealthCheck&gt;("serialization");
/// </code>
/// <para>
/// See the pluggable serialization architecture documentation.
/// </para>
/// </remarks>
public sealed partial class SerializationHealthCheck : IHealthCheck
{
	/// <summary>
	/// Maximum time allowed for a single round-trip before flagging as degraded.
	/// </summary>
	private static readonly TimeSpan DegradedThreshold = TimeSpan.FromMilliseconds(50);

	private readonly ISerializerRegistry _registry;
	private readonly ILogger<SerializationHealthCheck> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationHealthCheck"/> class.
	/// </summary>
	/// <param name="registry">The serializer registry.</param>
	/// <param name="logger">The logger instance.</param>
	/// <exception cref="ArgumentNullException">Thrown when registry or logger is null.</exception>
	public SerializationHealthCheck(
		ISerializerRegistry registry,
		ILogger<SerializationHealthCheck> logger)
	{
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Status of serializer verification.
	/// </summary>
	private enum VerificationStatus
	{
		Passed,
		Slow,
		Failed
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>();
		var failures = new List<string>();
		var degraded = new List<string>();

		// Check 1: Verify current serializer is configured.
		byte currentId;
		IPluggableSerializer currentSerializer;
		try
		{
			(currentId, currentSerializer) = _registry.GetCurrent();
			data["current_serializer"] = $"{currentSerializer.Name} (0x{currentId:X2})";
		}
		catch (InvalidOperationException ex)
		{
			LogNoCurrentSerializerConfigured(ex);

			return Task.FromResult(HealthCheckResult.Unhealthy(
				description: "No current serializer configured.",
				exception: ex,
				data: data));
		}

		// Check 2: Verify all registered serializers can perform round-trip.
		var allSerializers = _registry.GetAll();
		data["total_registered"] = allSerializers.Count;

		var verifiedCount = 0;
		foreach (var (id, name, serializer) in allSerializers)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var result = VerifySerializer(id, name, serializer);
			data[$"serializer_{name}"] = result.ToString();

			switch (result.Status)
			{
				case VerificationStatus.Passed:
					verifiedCount++;
					break;

				case VerificationStatus.Slow:
					verifiedCount++;
					degraded.Add($"{name}: slow ({result.DurationMs}ms > {DegradedThreshold.TotalMilliseconds}ms)");
					break;

				case VerificationStatus.Failed:
					failures.Add($"{name}: {result.ErrorMessage}");
					break;

				default:
					failures.Add($"{name}: {result.ErrorMessage}");
					break;
			}
		}

		data["verified_count"] = verifiedCount;
		data["failed_count"] = failures.Count;

		// Determine overall health status.
		if (failures.Count > 0)
		{
			var description = $"Serialization verification failed for {failures.Count} serializer(s): " +
				string.Join("; ", failures);

			LogHealthCheckDegradedFailures(failures.Count, allSerializers.Count);

			return Task.FromResult(HealthCheckResult.Degraded(
				description: description,
				data: data));
		}

		if (degraded.Count > 0)
		{
			var description = $"Serialization is slow for {degraded.Count} serializer(s): " +
				string.Join("; ", degraded);

			LogHealthCheckDegradedSlow(degraded.Count);

			return Task.FromResult(HealthCheckResult.Degraded(
				description: description,
				data: data));
		}

		LogHealthCheckPassed(verifiedCount);

		return Task.FromResult(HealthCheckResult.Healthy(
			description: $"All {verifiedCount} registered serializers verified successfully.",
			data: data));
	}

	/// <summary>
	/// Verifies a serializer by performing a round-trip serialization test.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Health check verification uses serializers with known types and does not run in trimmed/AOT scenarios.")]
	[UnconditionalSuppressMessage(
		"AotAnalysis",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Health check verification uses serializers with known types and does not run in trimmed/AOT scenarios.")]
	private SerializerVerificationResult VerifySerializer(
		byte id,
		string name,
		IPluggableSerializer serializer)
	{
		// Use simple string for verification - works with all serializers without registration.
		var testValue = $"HealthCheck_{Guid.NewGuid():N}";

		var startTime = DateTimeOffset.UtcNow;
		try
		{
			// Serialize.
			var serialized = serializer.Serialize(testValue);

			// Deserialize.
			var deserialized = serializer.Deserialize<string>(serialized);

			var duration = DateTimeOffset.UtcNow - startTime;
			var durationMs = duration.TotalMilliseconds;

			// Verify round-trip integrity.
			if (deserialized != testValue)
			{
				return new SerializerVerificationResult(
					VerificationStatus.Failed,
					durationMs,
					"Round-trip data mismatch");
			}

			// Check for slow serialization.
			if (duration > DegradedThreshold)
			{
				return new SerializerVerificationResult(
					VerificationStatus.Slow,
					durationMs,
					null);
			}

			return new SerializerVerificationResult(
				VerificationStatus.Passed,
				durationMs,
				null);
		}
		catch (Exception ex)
		{
			LogSerializerVerificationFailed(ex, name, id);

			var duration = DateTimeOffset.UtcNow - startTime;
			return new SerializerVerificationResult(
				VerificationStatus.Failed,
				duration.TotalMilliseconds,
				ex.Message);
		}
	}

	#region LoggerMessage Definitions

	[LoggerMessage(LogLevel.Error, "Serialization health check failed: no current serializer configured")]
	private partial void LogNoCurrentSerializerConfigured(Exception exception);

	[LoggerMessage(
		LogLevel.Warning,
		"Serialization health check degraded: {FailureCount} failures out of {TotalCount} serializers")]
	private partial void LogHealthCheckDegradedFailures(int failureCount, int totalCount);

	[LoggerMessage(LogLevel.Warning, "Serialization health check degraded: {SlowCount} slow serializers")]
	private partial void LogHealthCheckDegradedSlow(int slowCount);

	[LoggerMessage(LogLevel.Debug, "Serialization health check passed: {VerifiedCount} serializers verified")]
	private partial void LogHealthCheckPassed(int verifiedCount);

	[LoggerMessage(LogLevel.Warning, "Serializer verification failed for '{SerializerName}' (0x{SerializerId:X2})")]
	private partial void LogSerializerVerificationFailed(
		Exception exception,
		string serializerName,
		byte serializerId);

	#endregion LoggerMessage Definitions

	/// <summary>
	/// Result of verifying a single serializer.
	/// </summary>
	private readonly record struct SerializerVerificationResult(
		VerificationStatus Status,
		double DurationMs,
		string? ErrorMessage)
	{
		public override string ToString() => Status switch
		{
			VerificationStatus.Passed => $"Passed ({DurationMs:F2}ms)",
			VerificationStatus.Slow => $"Slow ({DurationMs:F2}ms)",
			VerificationStatus.Failed => $"Failed: {ErrorMessage}",
			_ => "Unknown"
		};
	}
}
