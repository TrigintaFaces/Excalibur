// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="FipsValidationService"/>.
/// Uses <see cref="IFipsDetector"/> mock to test FIPS state scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class FipsValidationServiceShould
{
	#region IsFipsEnabled Tests

	[Fact]
	public void IsFipsEnabled_ReturnsTrue_WhenDetectorReportsEnabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.IsFipsEnabled).Returns(true);

		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsFipsEnabled;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsFipsEnabled_ReturnsFalse_WhenDetectorReportsDisabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.IsFipsEnabled).Returns(false);

		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsFipsEnabled;

		// Assert
		result.ShouldBeFalse();
	}

	#endregion IsFipsEnabled Tests

	#region DetectionResult Tests

	[Fact]
	public void DetectionResult_ReturnsDetectorStatus()
	{
		// Arrange
		var expectedResult = FipsDetectionResult.Enabled("Windows", "CryptoConfig.AllowOnlyFipsAlgorithms = true");
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.GetStatus()).Returns(expectedResult);

		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.DetectionResult;

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion DetectionResult Tests

	#region RequireFipsCompliance Tests

	[Fact]
	public void RequireFipsCompliance_DoesNotThrow_WhenFipsEnabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.IsFipsEnabled).Returns(true);

		var sut = new FipsValidationService(detector);

		// Act & Assert
		Should.NotThrow(() => sut.RequireFipsCompliance());
	}

	[Fact]
	public void RequireFipsCompliance_ThrowsEncryptionException_WhenFipsDisabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.IsFipsEnabled).Returns(false);
		_ = A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Disabled("Windows", "FIPS mode not configured in system policy"));

		var sut = new FipsValidationService(detector);

		// Act & Assert
		var ex = Should.Throw<EncryptionException>(() => sut.RequireFipsCompliance());
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.FipsComplianceViolation);
		ex.Message.ShouldContain("FIPS 140-2 compliance required but not enabled");
	}

	#endregion RequireFipsCompliance Tests

	#region IsAlgorithmFipsCompliant Tests

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, true)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, true)]
	public void IsAlgorithmFipsCompliant_ReturnsExpectedResult(EncryptionAlgorithm algorithm, bool expectedCompliant)
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsAlgorithmFipsCompliant(algorithm);

		// Assert
		result.ShouldBe(expectedCompliant);
	}

	[Fact]
	public void IsAlgorithmFipsCompliant_ReturnsFalse_ForUnknownAlgorithm()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsAlgorithmFipsCompliant((EncryptionAlgorithm)999);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion IsAlgorithmFipsCompliant Tests

	#region IsKeySizeFipsCompliant Tests

	[Theory]
	[InlineData(128, true)]
	[InlineData(192, true)]
	[InlineData(256, true)]
	[InlineData(64, false)]
	[InlineData(512, false)]
	[InlineData(0, false)]
	public void IsKeySizeFipsCompliant_ReturnsExpectedResult(int keySizeBits, bool expectedCompliant)
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsKeySizeFipsCompliant(keySizeBits);

		// Assert
		result.ShouldBe(expectedCompliant);
	}

	#endregion IsKeySizeFipsCompliant Tests

	#region GetComplianceReport Tests

	[Fact]
	public void GetComplianceReport_ReturnsCompliantReport_WhenFipsEnabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Enabled("Windows", "Registry key FIPS enabled"));

		var sut = new FipsValidationService(detector);

		// Act
		var report = sut.GetComplianceReport();

		// Assert
		_ = report.ShouldNotBeNull();
		report.IsCompliant.ShouldBeTrue();
		report.Platform.ShouldBe("Windows");
		report.ValidationDetails.ShouldContain("Registry key FIPS enabled");
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		report.CheckedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void GetComplianceReport_ReturnsNonCompliantReport_WhenFipsDisabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Disabled("Linux", "/proc/sys/crypto/fips_enabled = 0"));

		var sut = new FipsValidationService(detector);

		// Act
		var report = sut.GetComplianceReport();

		// Assert
		report.IsCompliant.ShouldBeFalse();
		report.Platform.ShouldBe("Linux");
	}

	[Fact]
	public void GetComplianceReport_IncludesApprovedAlgorithms()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		_ = A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Enabled("Windows", "Test"));

		var sut = new FipsValidationService(detector);

		// Act
		var report = sut.GetComplianceReport();

		// Assert
		report.ApprovedAlgorithms.ShouldNotBeEmpty();
		report.ApprovedAlgorithms.Count.ShouldBe(2);

		// Verify AES-256-GCM entry
		var aesGcm = report.ApprovedAlgorithms.FirstOrDefault(a => a.Algorithm == EncryptionAlgorithm.Aes256Gcm);
		_ = aesGcm.ShouldNotBeNull();
		aesGcm.IsApproved.ShouldBeTrue();
		aesGcm.Standard.ShouldBe("NIST SP 800-38D");
	}

	#endregion GetComplianceReport Tests

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullDetector_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new FipsValidationService(null!));
	}

	#endregion Constructor Tests
}
