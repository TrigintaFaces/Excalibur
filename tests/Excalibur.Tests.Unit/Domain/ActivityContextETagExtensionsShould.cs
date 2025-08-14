using Excalibur.Core.Concurrency;
using Excalibur.Domain;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class ActivityContextETagExtensionsShould
{
	[Fact]
	public void ReturnNullWhenETagIsNotSet()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(null);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnIncomingValueWhenSet()
	{
		// Arrange
		const string expected = "W/\"123\"";
		var context = A.Fake<IActivityContext>();
		var etag = A.Fake<IETag>();
		A.CallTo(() => etag.IncomingValue).Returns(expected);
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(etag);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void SetOutgoingValueWhenETagMethodCalledWithValue()
	{
		// Arrange
		const string newValue = "W/\"456\"";
		var context = A.Fake<IActivityContext>();
		var etag = A.Fake<IETag>();
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(etag);

		// Act
		context.ETag(newValue);

		// Assert
		A.CallToSet(() => etag.OutgoingValue).To(newValue).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.ETag())
			.ParamName.ShouldBe("context");

		Should.Throw<ArgumentNullException>(() => context.ETag("some-etag"))
			.ParamName.ShouldBe("context");

		Should.Throw<ArgumentNullException>(() => context.LatestETag())
			.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ReturnLatestETagAsOutgoingWhenBothAreSet()
	{
		// Arrange
		const string incoming = "W/\"123\"";
		const string outgoing = "W/\"456\"";
		var context = A.Fake<IActivityContext>();
		var etag = A.Fake<IETag>();
		A.CallTo(() => etag.IncomingValue).Returns(incoming);
		A.CallTo(() => etag.OutgoingValue).Returns(outgoing);
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(etag);

		// Act
		var result = context.LatestETag();

		// Assert
		result.ShouldBe(outgoing);
	}

	[Fact]
	public void ReturnLatestETagAsIncomingWhenOutgoingNotSet()
	{
		// Arrange
		const string incoming = "W/\"123\"";
		var context = A.Fake<IActivityContext>();
		var etag = A.Fake<IETag>();
		A.CallTo(() => etag.IncomingValue).Returns(incoming);
		A.CallTo(() => etag.OutgoingValue).Returns(string.Empty);
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(etag);

		// Act
		var result = context.LatestETag();

		// Assert
		result.ShouldBe(incoming);
	}

	[Fact]
	public void ReturnNullForLatestETagWhenNeitherSet()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.Get(nameof(ETag), A<IETag?>._)).Returns(null);

		// Act
		var result = context.LatestETag();

		// Assert
		result.ShouldBeNull();
	}
}
