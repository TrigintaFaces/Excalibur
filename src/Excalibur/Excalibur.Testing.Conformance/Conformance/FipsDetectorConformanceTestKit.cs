// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IFipsDetector"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit validates that implementations of <see cref="IFipsDetector"/>
/// correctly implement FIPS 140-2/140-3 detection contracts. IFipsDetector is the SIMPLEST
/// interface in the conformance kit series - only 1 property + 1 method.
/// </para>
/// <para>
/// <strong>DETECTION PATTERN:</strong> IFipsDetector provides read-only detection capabilities
/// with caching. Implementations typically cache the detection result using <see cref="Lazy{T}"/>
/// to avoid repeated detection overhead.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>IsFipsEnabled returns a boolean without throwing</description></item>
/// <item><description>GetStatus returns a non-null FipsDetectionResult with all required properties</description></item>
/// <item><description>IsFipsEnabled matches GetStatus().IsFipsEnabled (consistency)</description></item>
/// <item><description>GetStatus returns consistent results (caching behavior)</description></item>
/// <item><description>FipsDetectionResult.Enabled() factory creates correct enabled result</description></item>
/// <item><description>FipsDetectionResult.Disabled() factory creates correct disabled result</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>NO DISPOSAL:</strong> Unlike metrics/telemetry kits, IFipsDetector is NOT IDisposable.
/// Detection results are immutable and cached, requiring no cleanup.
/// </para>
/// <para>
/// To use this kit:
/// <list type="number">
/// <item><description>Inherit from this class</description></item>
/// <item><description>Implement <see cref="CreateDetector"/> to return your IFipsDetector implementation</description></item>
/// <item><description>Create [Fact] test methods that call the protected test methods</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyFipsDetectorConformanceTests : FipsDetectorConformanceTestKit
/// {
///     protected override IFipsDetector CreateDetector() =&gt;
///         new MyFipsDetector(NullLogger&lt;MyFipsDetector&gt;.Instance);
///
///     [Fact]
///     public void IsFipsEnabled_ShouldReturnBooleanWithoutThrowing_Test() =&gt;
///         IsFipsEnabled_ShouldReturnBooleanWithoutThrowing();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class FipsDetectorConformanceTestKit
{
	/// <summary>
	/// Creates an instance of the <see cref="IFipsDetector"/> implementation under test.
	/// </summary>
	/// <returns>A new instance of the detector implementation.</returns>
	/// <remarks>
	/// Implementations should return a fully configured detector instance.
	/// DefaultFipsDetector requires an ILogger dependency which can be satisfied
	/// with NullLogger&lt;DefaultFipsDetector&gt;.Instance.
	/// </remarks>
	protected abstract IFipsDetector CreateDetector();

	#region IsFipsEnabled Property Tests

	/// <summary>
	/// Verifies that <see cref="IFipsDetector.IsFipsEnabled"/> returns a boolean without throwing.
	/// </summary>
	/// <remarks>
	/// The property should return either true or false depending on the platform's
	/// FIPS configuration. It should never throw an exception.
	/// </remarks>
	protected virtual void IsFipsEnabled_ShouldReturnBooleanWithoutThrowing()
	{
		// Arrange
		var detector = CreateDetector();

		// Act - Should not throw
		var result = detector.IsFipsEnabled;

		// Assert - result is a boolean (true or false), implicit by type system
		// If we get here without exception, the test passes
		_ = result; // Use the result to avoid compiler warning
	}

	#endregion

	#region GetStatus Method Tests

	/// <summary>
	/// Verifies that <see cref="IFipsDetector.GetStatus"/> returns a non-null result.
	/// </summary>
	/// <remarks>
	/// GetStatus should never return null. It must always return a valid
	/// FipsDetectionResult with all required properties populated.
	/// </remarks>
	protected virtual void GetStatus_ShouldReturnNonNullResult()
	{
		// Arrange
		var detector = CreateDetector();

		// Act
		var result = detector.GetStatus();

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetStatus() to return non-null result.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IFipsDetector.GetStatus"/> returns a result with all required properties.
	/// </summary>
	/// <remarks>
	/// FipsDetectionResult has required properties: IsFipsEnabled, Platform, ValidationDetails.
	/// All must be populated (Platform and ValidationDetails should not be null).
	/// </remarks>
	protected virtual void GetStatus_ShouldReturnResultWithRequiredProperties()
	{
		// Arrange
		var detector = CreateDetector();

		// Act
		var result = detector.GetStatus();

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetStatus() to return non-null result.");
		}

		if (result.Platform == null)
		{
			throw new TestFixtureAssertionException(
				"Expected Platform property to be non-null.");
		}

		if (result.ValidationDetails == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidationDetails property to be non-null.");
		}

		if (result.DetectedAt == default)
		{
			throw new TestFixtureAssertionException(
				"Expected DetectedAt property to be set to a non-default value.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IFipsDetector.IsFipsEnabled"/> matches <see cref="IFipsDetector.GetStatus"/>.IsFipsEnabled.
	/// </summary>
	/// <remarks>
	/// The IsFipsEnabled property and GetStatus().IsFipsEnabled must be consistent.
	/// They should always return the same value for the same detector instance.
	/// </remarks>
	protected virtual void IsFipsEnabled_ShouldMatchGetStatusResult()
	{
		// Arrange
		var detector = CreateDetector();

		// Act
		var propertyValue = detector.IsFipsEnabled;
		var statusValue = detector.GetStatus().IsFipsEnabled;

		// Assert
		if (propertyValue != statusValue)
		{
			throw new TestFixtureAssertionException(
				$"Expected IsFipsEnabled property ({propertyValue}) to match GetStatus().IsFipsEnabled ({statusValue}).");
		}
	}

	/// <summary>
	/// Verifies that multiple calls to <see cref="IFipsDetector.GetStatus"/> return consistent DetectedAt.
	/// </summary>
	/// <remarks>
	/// Implementations typically cache the detection result using Lazy&lt;T&gt;.
	/// Multiple calls should return the same DetectedAt timestamp, indicating
	/// the result was cached and not re-detected.
	/// </remarks>
	protected virtual void GetStatus_MultipleCalls_ShouldReturnConsistentDetectedAt()
	{
		// Arrange
		var detector = CreateDetector();

		// Act
		var result1 = detector.GetStatus();
		var result2 = detector.GetStatus();

		// Assert - same DetectedAt indicates caching
		if (result1.DetectedAt != result2.DetectedAt)
		{
			throw new TestFixtureAssertionException(
				$"Expected multiple GetStatus() calls to return same DetectedAt (caching), " +
				$"but got {result1.DetectedAt} and {result2.DetectedAt}.");
		}
	}

	#endregion

	#region FipsDetectionResult Factory Method Tests

	/// <summary>
	/// Verifies that <see cref="FipsDetectionResult.Enabled"/> factory creates correct enabled result.
	/// </summary>
	/// <remarks>
	/// The Enabled factory method should create a result with IsFipsEnabled = true
	/// and the provided platform and details values.
	/// </remarks>
	protected virtual void FipsDetectionResult_Enabled_ShouldCreateCorrectResult()
	{
		// Arrange
		const string platform = "TestPlatform";
		const string details = "Test validation details";

		// Act
		var result = FipsDetectionResult.Enabled(platform, details);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected FipsDetectionResult.Enabled() to return non-null result.");
		}

		if (!result.IsFipsEnabled)
		{
			throw new TestFixtureAssertionException(
				"Expected FipsDetectionResult.Enabled() to create result with IsFipsEnabled = true.");
		}

		if (result.Platform != platform)
		{
			throw new TestFixtureAssertionException(
				$"Expected Platform to be '{platform}', but got '{result.Platform}'.");
		}

		if (result.ValidationDetails != details)
		{
			throw new TestFixtureAssertionException(
				$"Expected ValidationDetails to be '{details}', but got '{result.ValidationDetails}'.");
		}

		if (result.DetectedAt == default)
		{
			throw new TestFixtureAssertionException(
				"Expected DetectedAt to be set to a non-default value.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="FipsDetectionResult.Disabled"/> factory creates correct disabled result.
	/// </summary>
	/// <remarks>
	/// The Disabled factory method should create a result with IsFipsEnabled = false
	/// and the provided platform and details values.
	/// </remarks>
	protected virtual void FipsDetectionResult_Disabled_ShouldCreateCorrectResult()
	{
		// Arrange
		const string platform = "TestPlatform";
		const string details = "FIPS not configured";

		// Act
		var result = FipsDetectionResult.Disabled(platform, details);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected FipsDetectionResult.Disabled() to return non-null result.");
		}

		if (result.IsFipsEnabled)
		{
			throw new TestFixtureAssertionException(
				"Expected FipsDetectionResult.Disabled() to create result with IsFipsEnabled = false.");
		}

		if (result.Platform != platform)
		{
			throw new TestFixtureAssertionException(
				$"Expected Platform to be '{platform}', but got '{result.Platform}'.");
		}

		if (result.ValidationDetails != details)
		{
			throw new TestFixtureAssertionException(
				$"Expected ValidationDetails to be '{details}', but got '{result.ValidationDetails}'.");
		}

		if (result.DetectedAt == default)
		{
			throw new TestFixtureAssertionException(
				"Expected DetectedAt to be set to a non-default value.");
		}
	}

	#endregion
}
