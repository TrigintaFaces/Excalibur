using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Domain;
using Excalibur.Tests.Fakes.Application;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class ActivityContextExtensionsShould
{
	[Fact]
	public void ReturnApplicationNameFromContext()
	{
		// Arrange
		const string expectedName = "TestApp";
		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.Get("ApplicationName", ApplicationContext.ApplicationName)).Returns(expectedName);

		// Act
		var result = context.ApplicationName();

		// Assert
		result.ShouldBe(expectedName);
	}

	[Fact]
	public void UseApplicationContextNameWhenNotInContext()
	{
		// Arrange
		var originalAppName = ApplicationContext.ApplicationName;
		try
		{
			// Set a known ApplicationContext.ApplicationName value
			ApplicationContext.Init(new Dictionary<string, string?> { { "ApplicationName", "DefaultAppName" } });

			var context = A.Fake<IActivityContext>();
			_ = A.CallTo(() => context.Get("ApplicationName", ApplicationContext.ApplicationName))
				.Returns(ApplicationContext.ApplicationName);

			// Act
			var result = context.ApplicationName();

			// Assert
			result.ShouldBe("DefaultAppName");
		}
		finally
		{
			// Reset ApplicationContext to its original state
			if (originalAppName != null)
			{
				ApplicationContext.Init(new Dictionary<string, string?> { { "ApplicationName", originalAppName } });
			}
		}
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullForApplicationName()
	{
		// Arrange
		IActivityContext? context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.ApplicationName());
		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullForClientAddress()
	{
		// Arrange
		IActivityContext? context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.ClientAddress());
		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullForConfiguration()
	{
		// Arrange
		IActivityContext? context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.Configuration());
		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public void SetAndRetrieveETagCorrectly()
	{
		// Arrange
		const string newETagValue = "W/\"12345\"";
		var etag = A.Fake<IETag>();
		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.Get(nameof(ETag), A<IETag>._)).Returns(etag);

		// Act
		context.ETag(newETagValue);

		// Assert
		_ = A.CallToSet(() => etag.OutgoingValue).To(newETagValue).MustHaveHappened();
	}

	[Fact]
	public void ReuseExistingETagWhenSetting()
	{
		// Arrange
		var existingETag = A.Fake<IETag>();
		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.Get(nameof(ETag), A<IETag>._)).Returns(existingETag);

		// Act
		context.ETag("test-etag");

		// Assert
		_ = A.CallTo(() => context.Get(nameof(ETag), A<IETag>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData("out-tag", "in-tag", "out-tag")]
	[InlineData(null, "in-tag", "in-tag")]
	[InlineData("", "in-tag", "in-tag")]
	public void ReturnCorrectLatestETag(string? outgoing, string? incoming, string? expected)
	{
		// Arrange
		var context = ActivityContextMother.WithETag(incoming, outgoing);

		// Act
		var result = context.LatestETag();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ReturnGenericValueFromContextByKey()
	{
		// Arrange
		const string key = "test-key";
		var expectedValue = new TestClass { Value = "test-value" };
		var context = A.Fake<IActivityContext>();
		_ = A.CallTo(() => context.Get(key, A<TestClass>._)).Returns(expectedValue);

		// Act
		var result = context.Get<TestClass>(key);

		// Assert
		result.ShouldBeSameAs(expectedValue);
	}

	private sealed class TestClass
	{
		public string? Value { get; set; }
	}
}
