namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class FipsValidationServiceShould
{
	[Fact]
	public void Report_fips_enabled_when_detector_says_enabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.IsFipsEnabled).Returns(true);
		var sut = new FipsValidationService(detector);

		// Act & Assert
		sut.IsFipsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void Report_fips_disabled_when_detector_says_disabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.IsFipsEnabled).Returns(false);
		var sut = new FipsValidationService(detector);

		// Act & Assert
		sut.IsFipsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void Not_throw_when_fips_is_enabled_and_required()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.IsFipsEnabled).Returns(true);
		var sut = new FipsValidationService(detector);

		// Act & Assert - should not throw
		sut.RequireFipsCompliance();
	}

	[Fact]
	public void Throw_when_fips_is_disabled_and_required()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.IsFipsEnabled).Returns(false);
		A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Disabled("Windows", "FIPS not enabled"));
		var sut = new FipsValidationService(detector);

		// Act & Assert
		var ex = Should.Throw<EncryptionException>(() => sut.RequireFipsCompliance());
		ex.ErrorCode.ShouldBe(EncryptionErrorCode.FipsComplianceViolation);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, true)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, true)]
	public void Validate_algorithm_fips_compliance(EncryptionAlgorithm algorithm, bool expected)
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsAlgorithmFipsCompliant(algorithm);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(128, true)]
	[InlineData(192, true)]
	[InlineData(256, true)]
	[InlineData(64, false)]
	[InlineData(512, false)]
	[InlineData(0, false)]
	public void Validate_key_size_fips_compliance(int keySizeBits, bool expected)
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.IsKeySizeFipsCompliant(keySizeBits);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Get_detection_result()
	{
		// Arrange
		var expectedResult = FipsDetectionResult.Enabled("Windows", "FIPS mode detected");
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.GetStatus()).Returns(expectedResult);
		var sut = new FipsValidationService(detector);

		// Act
		var result = sut.DetectionResult;

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public void Generate_compliance_report_when_fips_enabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Enabled("Windows", "FIPS enabled via policy"));
		var sut = new FipsValidationService(detector);

		// Act
		var report = sut.GetComplianceReport();

		// Assert
		report.IsCompliant.ShouldBeTrue();
		report.Platform.ShouldBe("Windows");
		report.ValidationDetails.ShouldBe("FIPS enabled via policy");
		report.CheckedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		report.ApprovedAlgorithms.Count.ShouldBe(2);

		var gcmAlgorithm = report.ApprovedAlgorithms.First(a => a.Algorithm == EncryptionAlgorithm.Aes256Gcm);
		gcmAlgorithm.IsApproved.ShouldBeTrue();
		gcmAlgorithm.Standard.ShouldBe("NIST SP 800-38D");

		var cbcAlgorithm = report.ApprovedAlgorithms.First(a => a.Algorithm == EncryptionAlgorithm.Aes256CbcHmac);
		cbcAlgorithm.IsApproved.ShouldBeTrue();
	}

	[Fact]
	public void Generate_compliance_report_when_fips_disabled()
	{
		// Arrange
		var detector = A.Fake<IFipsDetector>();
		A.CallTo(() => detector.GetStatus()).Returns(
			FipsDetectionResult.Disabled("Linux", "FIPS not configured"));
		var sut = new FipsValidationService(detector);

		// Act
		var report = sut.GetComplianceReport();

		// Assert
		report.IsCompliant.ShouldBeFalse();
		report.Platform.ShouldBe("Linux");
	}

	[Fact]
	public void Throw_for_null_fips_detector()
	{
		Should.Throw<ArgumentNullException>(() => new FipsValidationService(null!));
	}
}
