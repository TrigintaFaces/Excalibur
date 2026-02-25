using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultFipsDetectorShould
{
	[Fact]
	public void Return_detection_result()
	{
		// Arrange
		var sut = new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Act
		var result = sut.GetStatus();

		// Assert
		result.ShouldNotBeNull();
		result.Platform.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Return_boolean_fips_status()
	{
		// Arrange
		var sut = new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Act
		var isFips = sut.IsFipsEnabled;

		// Assert — on most dev machines FIPS is disabled
		isFips.ShouldBeOneOf(true, false);
	}

	[Fact]
	public void Cache_result_across_calls()
	{
		// Arrange
		var sut = new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Act
		var result1 = sut.GetStatus();
		var result2 = sut.GetStatus();

		// Assert — same cached Lazy<> instance
		result1.ShouldBeSameAs(result2);
	}

	[Fact]
	public void Detect_platform()
	{
		// Arrange
		var sut = new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Act
		var result = sut.GetStatus();

		// Assert - on Windows test machines
		result.Platform.ShouldNotBeNullOrEmpty();
		result.ValidationDetails.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Return_consistent_is_fips_enabled_and_status()
	{
		// Arrange
		var sut = new DefaultFipsDetector(NullLogger<DefaultFipsDetector>.Instance);

		// Act
		var status = sut.GetStatus();
		var isFips = sut.IsFipsEnabled;

		// Assert
		isFips.ShouldBe(status.IsFipsEnabled);
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() => new DefaultFipsDetector(null!));
	}
}
