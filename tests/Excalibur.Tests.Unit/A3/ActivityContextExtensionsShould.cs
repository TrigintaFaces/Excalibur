using Excalibur.A3;
using Excalibur.Core.Concurrency;
using Excalibur.Domain;
using Excalibur.Tests.Fakes.Application;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.Unit.A3;

public class ActivityContextExtensionsShould
{
	[Fact]
	public void AccessTokenReturnsTokenFromContext()
	{
		// Arrange
		var accessToken = A.Fake<IAccessToken>();
		var context = ActivityContextMother.WithAccessToken(accessToken);

		// Act
		var result = context.AccessToken();

		// Assert
		result.ShouldBeSameAs(accessToken);
	}

	[Fact]
	public void AccessTokenReturnsNullWhenNotInContext()
	{
		// Arrange
		var context = ActivityContextMother.WithAccessToken(null);

		// Act
		var result = context.AccessToken();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void AccessTokenThrowsArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.AccessToken());
		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ReturnTenantIdFromContext()
	{
		// Arrange
		const string expectedTenantId = "my-tenant-id";
		var context = ActivityContextMother.WithTenantId(expectedTenantId);

		// Act
		var result = context.TenantId();

		// Assert
		result.ShouldBe(expectedTenantId);
	}

	[Fact]
	public void ReturnCorrelationIdFromContext()
	{
		// Arrange
		var expectedId = Guid.NewGuid();
		var context = ActivityContextMother.WithCorrelationId(expectedId);

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldBe(expectedId);
	}

	[Fact]
	public void ReturnIncomingETagFromContext()
	{
		// Arrange
		const string expected = "incoming-etag";
		var context = ActivityContextMother.WithETag(incoming: expected);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void SetOutgoingETag()
	{
		// Arrange
		var etag = A.Fake<IETag>();
		var context = A.Fake<IActivityContext>();

		_ = A.CallTo(() => context.Get(nameof(ETag), A<IETag>._)).Returns(etag);

		// Act
		context.ETag("new-value");

		// Assert
		_ = A.CallToSet(() => etag.OutgoingValue).To("new-value").MustHaveHappened();
	}

	[Theory]
	[InlineData("out", "in", "out")]
	[InlineData("", "in", "in")]
	[InlineData(null, "in", "in")]
	[InlineData(null, null, null)]
	public void ReturnLatestETagCorrectly(string? outgoing, string? incoming, string? expected)
	{
		// Arrange
		var context = ActivityContextMother.WithETag(incoming, outgoing);

		// Act
		var result = context.LatestETag();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ReturnClientAddressFromContext()
	{
		// Arrange
		var context = ActivityContextMother.WithClientAddress("10.0.0.5");

		// Act
		var result = context.ClientAddress();

		// Assert
		result.ShouldBe("10.0.0.5");
	}

	[Fact]
	public void ReturnConfigurationFromContext()
	{
		// Arrange
		var config = A.Fake<IConfiguration>();
		var context = ActivityContextMother.WithConfiguration(config);

		// Act
		var result = context.Configuration();

		// Assert
		result.ShouldBe(config);
	}

	[Fact]
	public void ReturnServiceProviderFromContext()
	{
		// Arrange
		var provider = A.Fake<IServiceProvider>();
		var context = ActivityContextMother.WithServiceProvider(provider);

		// Act
		var result = context.ServiceProvider();

		// Assert
		result.ShouldBe(provider);
	}

	[Fact]
	public void ApplicationNameThrowsWhenContextIsNull()
	{
		// Arrange
		IActivityContext context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.ApplicationName());
		exception.ParamName.ShouldBe("context");
	}

	[Fact]
	public void GetGenericValueFromContextByKey()
	{
		// Arrange
		const string key = "CustomKey";
		var expectedValue = "CustomValue";
		var context = A.Fake<IActivityContext>();

		_ = A.CallTo(() => context.Get(key, A<string>._)).Returns(expectedValue);

		// Act
		var result = context.Get<string>(key);

		// Assert
		result.ShouldBe(expectedValue);
	}

	[Fact]
	public void GetThrowsWhenContextIsNull()
	{
		// Arrange
		IActivityContext? context = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => context.Get<string>("any"));
		exception.ParamName.ShouldBe("context");
	}
}
