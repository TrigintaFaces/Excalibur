// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IEncryptionTelemetry conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateTelemetry"/> to verify that
/// your encryption telemetry implementation conforms to the IEncryptionTelemetry contract.
/// </para>
/// <para>
/// The test kit verifies core telemetry collection operations including:
/// <list type="bullet">
/// <item><description>Meter property returns non-null with correct name</description></item>
/// <item><description>RecordOperation with null validation</description></item>
/// <item><description>RecordOperationDuration with null validation</description></item>
/// <item><description>UpdateProviderHealth with null validation</description></item>
/// <item><description>RecordFieldsMigrated with count=0 and count>0</description></item>
/// <item><description>RecordKeyRotation with null validation</description></item>
/// <item><description>RecordBytesProcessed with bytes=0 and bytes>0</description></item>
/// <item><description>RecordCacheAccess with hit=true and hit=false</description></item>
/// <item><description>UpdateActiveKeyCount completes successfully</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>TELEMETRY PATTERN:</strong> IEncryptionTelemetry is an OpenTelemetry-based telemetry
/// collection interface. Methods are void (fire-and-forget) but use STRICT null validation -
/// all string parameters throw ArgumentNullException on null.
/// </para>
/// <para>
/// <strong>PARAMETERLESS CONSTRUCTOR:</strong> EncryptionTelemetry requires no dependencies,
/// making this a simple conformance kit constructor.
/// </para>
/// <para>
/// <strong>IDisposable:</strong> EncryptionTelemetry implements IDisposable to cleanup the
/// internal Meter. Tests MUST call Cleanup to properly dispose.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class EncryptionTelemetryConformanceTests : EncryptionTelemetryConformanceTestKit
/// {
///     protected override IEncryptionTelemetry CreateTelemetry()
///     {
///         return new EncryptionTelemetry();
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class EncryptionTelemetryConformanceTestKit
{
	/// <summary>
	/// Creates a fresh encryption telemetry instance for testing.
	/// </summary>
	/// <returns>An IEncryptionTelemetry implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For EncryptionTelemetry, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IEncryptionTelemetry CreateTelemetry() =&gt;
	///     new EncryptionTelemetry();
	/// </code>
	/// </remarks>
	protected abstract IEncryptionTelemetry CreateTelemetry();

	/// <summary>
	/// Cleanup after each test. Disposes the telemetry if IDisposable.
	/// </summary>
	/// <param name="telemetry">The telemetry instance to cleanup.</param>
	protected virtual void Cleanup(IEncryptionTelemetry? telemetry)
	{
		if (telemetry is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Gets the default provider name for tests.
	/// </summary>
	protected virtual string DefaultProvider => "test-provider";

	/// <summary>
	/// Gets the default operation name for tests.
	/// </summary>
	protected virtual string DefaultOperation => "Encrypt";

	/// <summary>
	/// Gets the default algorithm name for tests.
	/// </summary>
	protected virtual string DefaultAlgorithm => "AES-256-GCM";

	#region Meter Property Tests

	/// <summary>
	/// Verifies that the Meter property returns a non-null instance with the correct name.
	/// </summary>
	protected virtual void Meter_ShouldBeNonNullAndNamed()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act
			var meter = telemetry.Meter;

			// Assert
			if (meter == null)
			{
				throw new TestFixtureAssertionException(
					"Expected Meter property to return non-null instance.");
			}

			if (meter.Name != "Excalibur.Dispatch.Encryption")
			{
				throw new TestFixtureAssertionException(
					$"Expected Meter.Name to be 'Excalibur.Dispatch.Encryption', but got '{meter.Name}'.");
			}
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region RecordOperation Tests

	/// <summary>
	/// Verifies that RecordOperation completes successfully with valid parameters.
	/// </summary>
	protected virtual void RecordOperation_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act - Should complete without throwing
			telemetry.RecordOperation(DefaultOperation, DefaultAlgorithm, "success", DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordOperation throws ArgumentNullException for null operation.
	/// </summary>
	protected virtual void RecordOperation_NullOperation_ShouldThrowArgumentNullException()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				telemetry.RecordOperation(null!, DefaultAlgorithm, "success", DefaultProvider);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RecordOperation to throw ArgumentNullException for null operation.");
			}
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region RecordOperationDuration Tests

	/// <summary>
	/// Verifies that RecordOperationDuration completes successfully with valid parameters.
	/// </summary>
	protected virtual void RecordOperationDuration_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act - Should complete without throwing
			telemetry.RecordOperationDuration(15.5, DefaultOperation, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordOperationDuration throws ArgumentNullException for null operation.
	/// </summary>
	protected virtual void RecordOperationDuration_NullOperation_ShouldThrowArgumentNullException()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				telemetry.RecordOperationDuration(15.5, null!, DefaultProvider);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RecordOperationDuration to throw ArgumentNullException for null operation.");
			}
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region UpdateProviderHealth Tests

	/// <summary>
	/// Verifies that UpdateProviderHealth completes successfully with valid parameters.
	/// </summary>
	protected virtual void UpdateProviderHealth_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing
			details.UpdateProviderHealth(DefaultProvider, "healthy", 100);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that UpdateProviderHealth throws ArgumentNullException for null provider.
	/// </summary>
	protected virtual void UpdateProviderHealth_NullProvider_ShouldThrowArgumentNullException()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act & Assert
			var caughtException = false;
			try
			{
				details.UpdateProviderHealth(null!, "healthy", 100);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected UpdateProviderHealth to throw ArgumentNullException for null provider.");
			}
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region RecordFieldsMigrated Tests

	/// <summary>
	/// Verifies that RecordFieldsMigrated completes successfully with count > 0.
	/// </summary>
	protected virtual void RecordFieldsMigrated_WithCount_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing (count > 0 records counter)
			details.RecordFieldsMigrated(1000, "old-provider", "new-provider", "user-store");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordFieldsMigrated completes successfully with count = 0.
	/// </summary>
	protected virtual void RecordFieldsMigrated_ZeroCount_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing (count = 0 skips increment)
			details.RecordFieldsMigrated(0, "old-provider", "new-provider", "user-store");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
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
		var telemetry = CreateTelemetry();
		try
		{
			// Act - Should complete without throwing
			telemetry.RecordKeyRotation(DefaultProvider, "scheduled");

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordKeyRotation throws ArgumentNullException for null provider.
	/// </summary>
	protected virtual void RecordKeyRotation_NullProvider_ShouldThrowArgumentNullException()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				telemetry.RecordKeyRotation(null!, "scheduled");
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected RecordKeyRotation to throw ArgumentNullException for null provider.");
			}
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region RecordBytesProcessed Tests

	/// <summary>
	/// Verifies that RecordBytesProcessed completes successfully with bytes > 0.
	/// </summary>
	protected virtual void RecordBytesProcessed_WithBytes_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act - Should complete without throwing (bytes > 0 records counter)
			telemetry.RecordBytesProcessed(1024, DefaultOperation, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordBytesProcessed completes successfully with bytes = 0.
	/// </summary>
	protected virtual void RecordBytesProcessed_ZeroBytes_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			// Act - Should complete without throwing (bytes = 0 skips increment)
			telemetry.RecordBytesProcessed(0, DefaultOperation, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region RecordCacheAccess Tests

	/// <summary>
	/// Verifies that RecordCacheAccess completes successfully with hit = true.
	/// </summary>
	protected virtual void RecordCacheAccess_Hit_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing
			details.RecordCacheAccess(hit: true, provider: DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	/// <summary>
	/// Verifies that RecordCacheAccess completes successfully with hit = false.
	/// </summary>
	protected virtual void RecordCacheAccess_Miss_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing
			details.RecordCacheAccess(hit: false, provider: DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion

	#region UpdateActiveKeyCount Tests

	/// <summary>
	/// Verifies that UpdateActiveKeyCount completes successfully with valid parameters.
	/// </summary>
	protected virtual void UpdateActiveKeyCount_ShouldCompleteSuccessfully()
	{
		// Arrange
		var telemetry = CreateTelemetry();
		try
		{
			if (telemetry.GetService(typeof(IEncryptionTelemetryDetails)) is not IEncryptionTelemetryDetails details)
			{
				throw new TestFixtureAssertionException(
					"Expected GetService(typeof(IEncryptionTelemetryDetails)) to return non-null instance.");
			}

			// Act - Should complete without throwing
			details.UpdateActiveKeyCount(5, DefaultProvider);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup(telemetry);
		}
	}

	#endregion
}
