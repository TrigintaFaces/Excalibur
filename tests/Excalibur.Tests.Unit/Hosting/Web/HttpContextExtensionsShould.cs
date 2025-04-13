using System.Net;

using Excalibur.Core;
using Excalibur.Hosting.Web;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting.Web;

public class HttpContextExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullCorrelationId()
	{
		// Arrange
		HttpContext context = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.CorrelationId())
			.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullETag()
	{
		// Arrange
		HttpContext context = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.ETag())
			.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullTenantId()
	{
		// Arrange
		HttpContext context = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.TenantId())
			.ParamName.ShouldBe("context");
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNullRemoteIpAddress()
	{
		// Arrange
		HttpContext context = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.RemoteIpAddress())
			.ParamName.ShouldBe("context");
	}

	[Fact]
	public void GetCorrelationIdFromHeaders()
	{
		// Arrange
		var expectedCorrelationId = Guid.NewGuid();
		var context = new DefaultHttpContext();
		context.Request.Headers[ExcaliburHeaderNames.CorrelationId] = new StringValues(expectedCorrelationId.ToString());

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldBe(expectedCorrelationId);
	}

	[Fact]
	public void GenerateNewCorrelationIdWhenNotInHeaders()
	{
		// Arrange
		var context = new DefaultHttpContext();

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateNewCorrelationIdWhenInvalidInHeaders()
	{
		// Arrange
		var context = new DefaultHttpContext();
		context.Request.Headers[ExcaliburHeaderNames.CorrelationId] = new StringValues("not-a-guid");

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GetETagFromIfMatchHeader()
	{
		// Arrange
		var expectedETag = "\"abc123\"";
		var context = new DefaultHttpContext();
		context.Request.Headers[HeaderNames.IfMatch] = new StringValues(expectedETag);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBe(expectedETag);
	}

	[Fact]
	public void GetETagFromIfNoneMatchHeader()
	{
		// Arrange
		var expectedETag = "\"xyz789\"";
		var context = new DefaultHttpContext();
		context.Request.Headers[HeaderNames.IfNoneMatch] = new StringValues(expectedETag);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBe(expectedETag);
	}

	[Fact]
	public void PreferIfMatchOverIfNoneMatchForETag()
	{
		// Arrange
		var ifMatchETag = "\"abc123\"";
		var ifNoneMatchETag = "\"xyz789\"";
		var context = new DefaultHttpContext();
		context.Request.Headers[HeaderNames.IfMatch] = new StringValues(ifMatchETag);
		context.Request.Headers[HeaderNames.IfNoneMatch] = new StringValues(ifNoneMatchETag);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBe(ifMatchETag);
	}

	[Fact]
	public void ReturnNullWhenNoETagHeadersPresent()
	{
		// Arrange
		var context = new DefaultHttpContext();

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnNullWhenNoRequestInContext()
	{
		// Arrange
		var context = A.Fake<HttpContext>();
		_ = A.CallTo(() => context.Request).Returns(null);

		// Act
		var result = context.ETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTenantIdFromRouteData()
	{
		// Arrange
		var expectedTenantId = "tenant-123";
		var context = new DefaultHttpContext();

		var routeData = new RouteData();
		routeData.Values["tenantId"] = expectedTenantId;

		var feature = new RouteValuesFeature { RouteValues = routeData.Values };
		context.Features.Set<IRouteValuesFeature>(feature);

		// Act
		var result = context.TenantId();

		// Assert
		result.ShouldBe(expectedTenantId);
	}

	[Fact]
	public void ReturnEmptyStringWhenTenantIdNotInRouteData()
	{
		// Arrange
		var context = new DefaultHttpContext();

		// Act
		var result = context.TenantId();

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void GetRemoteIpAddress()
	{
		// Arrange
		var expectedIp = "192.168.1.1";
		var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(expectedIp) } };

		// Act
		var result = context.RemoteIpAddress();

		// Assert
		result.ShouldBe(expectedIp);
	}

	[Fact]
	public void ReturnNullWhenNoConnectionAvailable()
	{
		// Arrange
		var context = A.Fake<HttpContext>();
		_ = A.CallTo(() => context.Connection).Returns(null);

		// Act
		var result = context.RemoteIpAddress();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnNullWhenNoRemoteIpAddressAvailable()
	{
		// Arrange
		var context = new DefaultHttpContext();
		context.Connection.RemoteIpAddress = null;

		// Act
		var result = context.RemoteIpAddress();

		// Assert
		result.ShouldBeNull();
	}
}
