using Excalibur.Core.Concurrency;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Concurrency;

public class ETagShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Arrange & Act
		var etag = new ETag();

		// Assert
		etag.IncomingValue.ShouldBeEmpty();
		etag.OutgoingValue.ShouldBeEmpty();
	}

	[Fact]
	public void SetAndGetIncomingValueCorrectly()
	{
		// Arrange
		var etag = new ETag();
		var expectedIncomingValue = "\"abc123\"";

		// Act
		etag.IncomingValue = expectedIncomingValue;

		// Assert
		etag.IncomingValue.ShouldBe(expectedIncomingValue);
	}

	[Fact]
	public void SetAndGetOutgoingValueCorrectly()
	{
		// Arrange
		var etag = new ETag();
		var expectedOutgoingValue = "\"xyz789\"";

		// Act
		etag.OutgoingValue = expectedOutgoingValue;

		// Assert
		etag.OutgoingValue.ShouldBe(expectedOutgoingValue);
	}

	[Fact]
	public void AllowNullValuesForIncomingValue()
	{
		// Arrange
		var etag = new ETag();

		// Act
		etag.IncomingValue = null;

		// Assert
		etag.IncomingValue.ShouldBeNull();
	}

	[Fact]
	public void AllowNullValuesForOutgoingValue()
	{
		// Arrange
		var etag = new ETag();

		// Act
		etag.OutgoingValue = null;

		// Assert
		etag.OutgoingValue.ShouldBeNull();
	}

	[Fact]
	public void SupportDifferentIncomingAndOutgoingValues()
	{
		// Arrange
		var etag = new ETag { IncomingValue = "\"in-123\"", OutgoingValue = "\"out-456\"" };

		// Assert
		etag.IncomingValue.ShouldBe("\"in-123\"");
		etag.OutgoingValue.ShouldBe("\"out-456\"");
	}

	[Fact]
	public void BeAssignableToIETag()
	{
		// Arrange
		IETag etag = new ETag();

		// Act & Assert
		_ = etag.ShouldNotBeNull();
		_ = etag.ShouldBeAssignableTo<IETag>();
	}

	[Fact]
	public void HaveEmptyStringAsDefaultIncomingValue()
	{
		var etag = new ETag();
		etag.IncomingValue.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyStringAsDefaultOutgoingValue()
	{
		var etag = new ETag();
		etag.OutgoingValue.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingAndGettingIncomingValue()
	{
		var etag = new ETag { IncomingValue = "W/\"12345\"" };
		etag.IncomingValue.ShouldBe("W/\"12345\"");
	}

	[Fact]
	public void AllowSettingAndGettingOutgoingValue()
	{
		var etag = new ETag { OutgoingValue = "W/\"67890\"" };
		etag.OutgoingValue.ShouldBe("W/\"67890\"");
	}

	[Fact]
	public void ImplementIETagInterface()
	{
		var etag = new ETag();
		_ = etag.ShouldBeAssignableTo<IETag>();
	}
}
