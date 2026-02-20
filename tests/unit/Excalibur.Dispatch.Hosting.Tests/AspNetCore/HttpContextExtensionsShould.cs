// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore;

/// <summary>
/// Unit tests for HttpContextExtensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpContextExtensionsShould : UnitTestBase
{
	[Fact]
	public void CorrelationId_WithValidHeader_ReturnsCorrelationId()
	{
		// Arrange
		var expectedGuid = Guid.NewGuid();
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.CorrelationId] = expectedGuid.ToString()
		});

		// Act
		var result = httpContext.CorrelationId();

		// Assert
		result.Value.ShouldBe(expectedGuid);
	}

	[Fact]
	public void CorrelationId_WithoutHeader_GeneratesNewCorrelationId()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>());

		// Act
		var result = httpContext.CorrelationId();

		// Assert
		result.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void CorrelationId_WithInvalidGuid_GeneratesNewCorrelationId()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.CorrelationId] = "not-a-guid"
		});

		// Act
		var result = httpContext.CorrelationId();

		// Assert
		result.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void CorrelationId_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.CorrelationId());
	}

	[Fact]
	public void CausationId_WithValidHeader_ReturnsCausationId()
	{
		// Arrange
		var expectedGuid = Guid.NewGuid();
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.CausationId] = expectedGuid.ToString()
		});

		// Act
		var result = httpContext.CausationId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe(expectedGuid);
	}

	[Fact]
	public void CausationId_WithoutHeader_ReturnsNull()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>());

		// Act
		var result = httpContext.CausationId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void TenantId_WithHeader_ReturnsTenantId()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.TenantId] = "tenant-123"
		});

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("tenant-123");
	}

	[Fact]
	public void TenantId_WithoutHeader_ReturnsNull()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var result = httpContext.TenantId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ETag_WithIfMatchHeader_ReturnsETagValue()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			["If-Match"] = "\"etag-value\""
		});

		// Act
		var result = httpContext.ETag();

		// Assert
		result.ShouldBe("\"etag-value\"");
	}

	[Fact]
	public void ETag_WithIfNoneMatchHeader_ReturnsETagValue()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			["If-None-Match"] = "\"etag-value\""
		});

		// Act
		var result = httpContext.ETag();

		// Assert
		result.ShouldBe("\"etag-value\"");
	}

	[Fact]
	public void ETag_WithNoHeader_ReturnsNull()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>());

		// Act
		var result = httpContext.ETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void CreateDispatchMessageContext_WithUnauthenticatedUser_ThrowsUnauthorizedAccessException()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act & Assert
		_ = Should.Throw<UnauthorizedAccessException>(httpContext.CreateDispatchMessageContext);
	}

	[Fact]
	public void CreateDispatchMessageContext_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.CreateDispatchMessageContext());
	}

	[Fact]
	public void CreateDispatchMessageContext_WithAuthenticatedUser_ReturnsMessageContext()
	{
		// Arrange
		var correlationGuid = Guid.NewGuid();
		var httpContext = CreateAuthenticatedHttpContext("user-123", correlationGuid);

		// Act
		var result = httpContext.CreateDispatchMessageContext();

		// Assert
		result.ShouldNotBeNull();
		result.Source.ShouldBe("WebRequest");
		result.CorrelationId.ShouldBe(correlationGuid.ToString());
		result.UserId.ShouldBe("user-123");
	}

	[Fact]
	public void CreateDispatchMessageContext_WithRequestHeaders_CopiesHeadersToItems()
	{
		// Arrange
		var correlationGuid = Guid.NewGuid();
		var httpContext = CreateAuthenticatedHttpContext("user-123", correlationGuid);
		httpContext.Request.Headers["X-Custom-Header"] = "custom-value";

		// Act
		var result = httpContext.CreateDispatchMessageContext();

		// Assert
		result.Items["X-Custom-Header"].ShouldBe("custom-value");
	}

	[Fact]
	public void CausationId_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.CausationId());
	}

	[Fact]
	public void CausationId_WithInvalidGuid_ReturnsNull()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.CausationId] = "not-a-guid"
		});

		// Act
		var result = httpContext.CausationId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void TenantId_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.TenantId());
	}

	[Fact]
	public void TenantId_WithRouteValue_ReturnsTenantId()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.RouteValues["tenantId"] = "route-tenant";

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("route-tenant");
	}

	[Fact]
	public void TenantId_WithQueryParameter_ReturnsTenantId()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.QueryString = new QueryString("?tenantId=query-tenant");

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("query-tenant");
	}

	[Fact]
	public void TenantId_WithClaim_ReturnsTenantId()
	{
		// Arrange
		var claims = new List<System.Security.Claims.Claim>
		{
			new("tenant_id", "claim-tenant")
		};
		var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
		var httpContext = new DefaultHttpContext
		{
			User = new System.Security.Claims.ClaimsPrincipal(identity)
		};

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("claim-tenant");
	}

	[Fact]
	public void TenantId_WithSubdomain_ReturnsTenantId()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("acme.example.com");

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("acme");
	}

	[Fact]
	public void TenantId_WithWwwSubdomain_ReturnsNull()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("www.example.com");

		// Act
		var result = httpContext.TenantId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void TenantId_WithAppSubdomain_ReturnsNull()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("app.example.com");

		// Act
		var result = httpContext.TenantId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void TenantId_HeaderTakesPrecedenceOverRouteValue()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			[WellKnownHeaderNames.TenantId] = "header-tenant"
		});
		httpContext.Request.RouteValues["tenantId"] = "route-tenant";

		// Act
		var result = httpContext.TenantId();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe("header-tenant");
	}

	[Fact]
	public void ETag_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.ETag());
	}

	[Fact]
	public void ETag_WithBothHeaders_ReturnsIfMatchFirst()
	{
		// Arrange
		var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
		{
			["If-Match"] = "\"if-match-etag\"",
			["If-None-Match"] = "\"if-none-match-etag\""
		});

		// Act
		var result = httpContext.ETag();

		// Assert
		result.ShouldBe("\"if-match-etag\"");
	}

	[Fact]
	public void RemoteIpAddress_WithNullHttpContext_ThrowsArgumentNullException()
	{
		// Arrange
		HttpContext? httpContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => httpContext.RemoteIpAddress());
	}

	[Fact]
	public void RemoteIpAddress_WithNoConnection_ReturnsNull()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var result = httpContext.RemoteIpAddress();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RemoteIpAddress_WithIpAddress_ReturnsIpString()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

		// Act
		var result = httpContext.RemoteIpAddress();

		// Assert
		result.ShouldBe("192.168.1.1");
	}

	private static HttpContext CreateHttpContextWithHeaders(Dictionary<string, string> headers)
	{
		var httpContext = new DefaultHttpContext();

		foreach (var header in headers)
		{
			httpContext.Request.Headers[header.Key] = header.Value;
		}

		return httpContext;
	}

	private static HttpContext CreateAuthenticatedHttpContext(string userId, Guid correlationId)
	{
		var claims = new List<System.Security.Claims.Claim>
		{
			new(System.Security.Claims.ClaimTypes.NameIdentifier, userId)
		};
		var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
		var principal = new System.Security.Claims.ClaimsPrincipal(identity);

		var services = new ServiceCollection();
		var serviceProvider = services.BuildServiceProvider();

		var httpContext = new DefaultHttpContext
		{
			User = principal,
			RequestServices = serviceProvider
		};
		httpContext.Request.Headers[WellKnownHeaderNames.CorrelationId] = correlationId.ToString();

		return httpContext;
	}
}
