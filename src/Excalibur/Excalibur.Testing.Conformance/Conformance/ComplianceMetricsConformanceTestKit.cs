// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IComplianceMetrics conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateMetrics"/> to verify that
/// your compliance metrics implementation conforms to the IComplianceMetrics contract.
/// </para>
/// <para>
/// The test kit verifies core metrics collection operations including:
/// <list type="bullet">
/// <item><description>Meter property returns non-null with correct name</description></item>
/// <item><description>RecordKeyRotation completes successfully</description></item>
/// <item><description>RecordKeyRotationFailure completes successfully</description></item>
/// <item><description>UpdateKeysNearingExpiration completes successfully</description></item>
/// <item><description>RecordEncryptionLatency with success true/false</description></item>
/// <item><description>RecordEncryptionOperation with and without bytes</description></item>
/// <item><description>RecordAuditEventLogged with and without tenantId</description></item>
/// <item><description>UpdateAuditBacklogSize completes successfully</description></item>
/// <item><description>RecordAuditIntegrityCheck with and without violations</description></item>
/// <item><description>RecordKeyUsage completes successfully</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>METRICS PATTERN:</strong> IComplianceMetrics is an OpenTelemetry-based metrics
/// collection interface. All methods are void (fire-and-forget) and lenient - they do NOT
/// throw on null inputs. Tests verify methods complete without exception.
/// </para>
/// <para>
/// <strong>PARAMETERLESS CONSTRUCTOR:</strong> ComplianceMetrics requires no dependencies,
/// making this the simplest conformance kit constructor.
/// </para>
/// <para>
/// <strong>IDisposable:</strong> ComplianceMetrics implements IDisposable to cleanup the
/// internal Meter. Tests MUST call Cleanup to properly dispose.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class ComplianceMetricsConformanceTests : ComplianceMetricsConformanceTestKit
/// {
///     protected override IComplianceMetrics CreateMetrics()
///     {
///         return new ComplianceMetrics();
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ComplianceMetricsConformanceTestKit
{
	/// <summary>
	/// Creates a fresh compliance metrics instance for testing.
	/// </summary>
	/// <returns>An IComplianceMetrics implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For ComplianceMetrics, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IComplianceMetrics CreateMetrics() =&gt;
	///     new ComplianceMetrics();
	/// </code>
	/// </remarks>
	protected abstract IComplianceMetrics CreateMetrics();

	/// <summary>
	/// Cleanup after each test. Disposes the metrics if IDisposable.
	/// </summary>
	/// <param name="metrics">The metrics instance to cleanup.</param>
	protected virtual void Cleanup(IComplianceMetrics? metrics)
	{
		if (metrics is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Generates a unique key ID for test isolation.
	/// </summary>
	/// <returns>A unique key identifier.</returns>
	protected virtual string GenerateKeyId() => $"test-key-{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the default provider name for tests.
	/// </summary>
	protected virtual string DefaultProvider => "test-provider";

	#region Meter Property Tests

	/// <summary>
	/// Verifies that the Meter property returns a non-null instance with the correct name.
	/// </summary>
	protected virtual void Meter_ShouldBeNonNullAndNamed()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act
			var meter = metrics.Meter;

			// Assert
			if (meter == null)
			{
				throw new TestFixtureAssertionException(
					"Expected Meter property to return non-null instance.");
			}

			if (meter.Name != "Excalibur.Dispatch.Compliance")
			{
				throw new TestFixtureAssertionException(
					$"Expected Meter.Name to be 'Excalibur.Dispatch.Compliance', but got '{meter.Name}'.");
			}
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordKeyRotation Tests

	/// <summary>
	/// Verifies that RecordKeyRotation completes successfully with valid parameters.
	/// </summary>
	protected virtual void RecordKeyRotation_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			var keyId = GenerateKeyId();

			// Act - Should complete without throwing
			metrics.RecordKeyRotation(keyId, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordKeyRotationFailure Tests

	/// <summary>
	/// Verifies that RecordKeyRotationFailure completes successfully with valid parameters.
	/// </summary>
	protected virtual void RecordKeyRotationFailure_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			var keyId = GenerateKeyId();

			// Act - Should complete without throwing
			metrics.RecordKeyRotationFailure(keyId, DefaultProvider, "TimeoutError");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region UpdateKeysNearingExpiration Tests

	/// <summary>
	/// Verifies that UpdateKeysNearingExpiration completes successfully.
	/// </summary>
	protected virtual void UpdateKeysNearingExpiration_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing
			metrics.UpdateKeysNearingExpiration(5, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordEncryptionLatency Tests

	/// <summary>
	/// Verifies that RecordEncryptionLatency completes successfully with success=true.
	/// </summary>
	protected virtual void RecordEncryptionLatency_SuccessTrue_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing
			metrics.RecordEncryptionLatency(
				durationMs: 15.5,
				operation: "Encrypt",
				provider: DefaultProvider,
				success: true);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	/// <summary>
	/// Verifies that RecordEncryptionLatency completes successfully with success=false.
	/// </summary>
	protected virtual void RecordEncryptionLatency_SuccessFalse_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing
			metrics.RecordEncryptionLatency(
				durationMs: 100.0,
				operation: "Decrypt",
				provider: DefaultProvider,
				success: false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordEncryptionOperation Tests

	/// <summary>
	/// Verifies that RecordEncryptionOperation completes successfully with bytes.
	/// </summary>
	protected virtual void RecordEncryptionOperation_WithBytes_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (sizeBytes > 0 records bytes counter)
			metrics.RecordEncryptionOperation(
				operation: "Encrypt",
				provider: DefaultProvider,
				sizeBytes: 1024);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	/// <summary>
	/// Verifies that RecordEncryptionOperation completes successfully with zero bytes.
	/// </summary>
	protected virtual void RecordEncryptionOperation_ZeroBytes_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (sizeBytes = 0 skips bytes counter)
			metrics.RecordEncryptionOperation(
				operation: "Decrypt",
				provider: DefaultProvider,
				sizeBytes: 0);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordAuditEventLogged Tests

	/// <summary>
	/// Verifies that RecordAuditEventLogged completes successfully with tenantId.
	/// </summary>
	protected virtual void RecordAuditEventLogged_WithTenant_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (tenantId adds tag)
			metrics.RecordAuditEventLogged(
				eventType: "UserLogin",
				outcome: "Success",
				tenantId: "tenant-123");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	/// <summary>
	/// Verifies that RecordAuditEventLogged completes successfully without tenantId.
	/// </summary>
	protected virtual void RecordAuditEventLogged_WithoutTenant_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (no tenantId tag)
			metrics.RecordAuditEventLogged(
				eventType: "ConfigChange",
				outcome: "Failure",
				tenantId: null);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region UpdateAuditBacklogSize Tests

	/// <summary>
	/// Verifies that UpdateAuditBacklogSize completes successfully.
	/// </summary>
	protected virtual void UpdateAuditBacklogSize_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing
			metrics.UpdateAuditBacklogSize(42);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordAuditIntegrityCheck Tests

	/// <summary>
	/// Verifies that RecordAuditIntegrityCheck completes successfully with violations.
	/// </summary>
	protected virtual void RecordAuditIntegrityCheck_WithViolations_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (violationsFound > 0 records violations counter)
			metrics.RecordAuditIntegrityCheck(
				eventsVerified: 1000,
				violationsFound: 2,
				durationMs: 500.0);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	/// <summary>
	/// Verifies that RecordAuditIntegrityCheck completes successfully without violations.
	/// </summary>
	protected virtual void RecordAuditIntegrityCheck_NoViolations_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			// Act - Should complete without throwing (violationsFound = 0 skips violations counter)
			metrics.RecordAuditIntegrityCheck(
				eventsVerified: 5000,
				violationsFound: 0,
				durationMs: 1200.0);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion

	#region RecordKeyUsage Tests

	/// <summary>
	/// Verifies that RecordKeyUsage completes successfully.
	/// </summary>
	protected virtual void RecordKeyUsage_ShouldCompleteSuccessfully()
	{
		// Arrange
		var metrics = CreateMetrics();
		try
		{
			var keyId = GenerateKeyId();

			// Act - Should complete without throwing
			metrics.RecordKeyUsage(keyId, DefaultProvider, "Encrypt");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(metrics);
		}
	}

	#endregion
}
