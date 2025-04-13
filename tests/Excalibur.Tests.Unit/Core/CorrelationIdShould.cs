using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class CorrelationIdShould
{
	[Fact]
	public void CreateCorrelationIdWithGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var correlationId = new CorrelationId(guid);

		// Assert
		correlationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void CreateCorrelationIdWithValidString()
	{
		// Arrange
		var guidString = Guid.NewGuid().ToString();

		// Act
		var correlationId = new CorrelationId(guidString);

		// Assert
		correlationId.Value.ToString().ShouldBe(guidString);
	}

	[Fact]
	public void ThrowExceptionForInvalidGuidString()
	{
		// Arrange
		var invalidGuidString = "invalid-guid";

		// Act & Assert
		_ = Should.Throw<FormatException>(() => new CorrelationId(invalidGuidString));
	}

	[Fact]
	public void CreateNewCorrelationIdWithoutParameters()
	{
		// Act
		var correlationId = new CorrelationId();

		// Assert
		correlationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void TestToStringOverride()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId = new CorrelationId(guid);

		// Act
		var result = correlationId.ToString();

		// Assert
		result.ShouldBe(guid.ToString());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TestNullOrWhitespaceStringThrowsArgumentNullException(string? input)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CorrelationId(input));
	}

	[Fact]
	public void TestSettingCorrelationIdValue()
	{
		// Arrange
		var correlationId = new CorrelationId();
		var newGuid = Guid.NewGuid();

		// Act
		correlationId.Value = newGuid;

		// Assert
		correlationId.Value.ShouldBe(newGuid);
	}
}
