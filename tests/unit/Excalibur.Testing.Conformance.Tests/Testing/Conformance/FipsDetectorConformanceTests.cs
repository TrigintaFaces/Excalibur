// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="DefaultFipsDetector"/> validating IFipsDetector contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// DefaultFipsDetector is the standard implementation that detects FIPS 140-2/140-3 mode
/// using platform-specific checks. It has a single ILogger dependency and uses Lazy&lt;T&gt;
/// for caching the detection result.
/// </para>
/// <para>
/// <strong>DETECTION PATTERN:</strong> IFipsDetector is a read-only detection interface.
/// The SIMPLEST interface in the conformance kit series with only 1 property + 1 method.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>IsFipsEnabled returns boolean without throwing</description></item>
/// <item><description>GetStatus returns non-null result with required properties</description></item>
/// <item><description>IsFipsEnabled matches GetStatus().IsFipsEnabled (consistency)</description></item>
/// <item><description>Multiple GetStatus calls return consistent DetectedAt (caching)</description></item>
/// <item><description>Factory methods create correct enabled/disabled results</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "DETECTION")]
public sealed class FipsDetectorConformanceTests : FipsDetectorConformanceTestKit
{
	/// <inheritdoc />
	protected override IFipsDetector CreateDetector() =>
		new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

	#region IsFipsEnabled Property Tests

	[Fact]
	public void IsFipsEnabled_ShouldReturnBooleanWithoutThrowing_Test() =>
		IsFipsEnabled_ShouldReturnBooleanWithoutThrowing();

	#endregion IsFipsEnabled Property Tests

	#region GetStatus Method Tests

	[Fact]
	public void GetStatus_ShouldReturnNonNullResult_Test() =>
		GetStatus_ShouldReturnNonNullResult();

	[Fact]
	public void GetStatus_ShouldReturnResultWithRequiredProperties_Test() =>
		GetStatus_ShouldReturnResultWithRequiredProperties();

	[Fact]
	public void IsFipsEnabled_ShouldMatchGetStatusResult_Test() =>
		IsFipsEnabled_ShouldMatchGetStatusResult();

	[Fact]
	public void GetStatus_MultipleCalls_ShouldReturnConsistentDetectedAt_Test() =>
		GetStatus_MultipleCalls_ShouldReturnConsistentDetectedAt();

	#endregion GetStatus Method Tests

	#region FipsDetectionResult Factory Method Tests

	[Fact]
	public void FipsDetectionResult_Enabled_ShouldCreateCorrectResult_Test() =>
		FipsDetectionResult_Enabled_ShouldCreateCorrectResult();

	[Fact]
	public void FipsDetectionResult_Disabled_ShouldCreateCorrectResult_Test() =>
		FipsDetectionResult_Disabled_ShouldCreateCorrectResult();

	#endregion FipsDetectionResult Factory Method Tests
}
