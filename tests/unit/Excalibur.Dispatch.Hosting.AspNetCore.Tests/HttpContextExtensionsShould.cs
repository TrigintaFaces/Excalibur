// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="HttpContextExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HttpContextExtensionsShould : UnitTestBase
{
	#region CorrelationId Tests

	[Fact]
	public void CorrelationId_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).CorrelationId());
	}

	[Fact]
	public void CorrelationId_ReturnNewGuid_WhenNoHeader()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var correlationId = httpContext.CorrelationId();

		// Assert
		correlationId.ShouldNotBeNull();
		correlationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void CorrelationId_ReturnHeaderValue_WhenPresent()
	{
		// Arrange
		var expected = Guid.NewGuid();
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Correlation-Id"] = expected.ToString();

		// Act
		var correlationId = httpContext.CorrelationId();

		// Assert
		correlationId.Value.ShouldBe(expected);
	}

	[Fact]
	public void CorrelationId_ReturnNewGuid_WhenHeaderIsInvalidGuid()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Correlation-Id"] = "not-a-guid";

		// Act
		var correlationId = httpContext.CorrelationId();

		// Assert
		correlationId.ShouldNotBeNull();
		correlationId.Value.ShouldNotBe(Guid.Empty);
	}

	#endregion

	#region CausationId Tests

	[Fact]
	public void CausationId_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).CausationId());
	}

	[Fact]
	public void CausationId_ReturnNull_WhenNoHeader()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var causationId = httpContext.CausationId();

		// Assert
		causationId.ShouldBeNull();
	}

	[Fact]
	public void CausationId_ReturnValue_WhenHeaderPresent()
	{
		// Arrange
		var expected = Guid.NewGuid();
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Causation-Id"] = expected.ToString();

		// Act
		var causationId = httpContext.CausationId();

		// Assert
		causationId.ShouldNotBeNull();
		causationId.Value.ShouldBe(expected);
	}

	[Fact]
	public void CausationId_ReturnNull_WhenHeaderIsInvalidGuid()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Causation-Id"] = "not-a-guid";

		// Act
		var causationId = httpContext.CausationId();

		// Assert
		causationId.ShouldBeNull();
	}

	#endregion

	#region TenantId Tests

	[Fact]
	public void TenantId_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).TenantId());
	}

	[Fact]
	public void TenantId_ReturnNull_WhenNoTenantInfo()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("localhost");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldBeNull();
	}

	[Fact]
	public void TenantId_ReturnFromHeader_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Tenant-Id"] = "tenant-123";

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Fact]
	public void TenantId_ReturnFromRouteValues_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.RouteValues["tenantId"] = "route-tenant";

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("route-tenant");
	}

	[Fact]
	public void TenantId_ReturnFromQueryString_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.QueryString = new QueryString("?tenantId=query-tenant");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("query-tenant");
	}

	[Fact]
	public void TenantId_ReturnFromClaim_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("localhost");
		var claims = new[] { new Claim("tenant_id", "claim-tenant") };
		httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("claim-tenant");
	}

	[Fact]
	public void TenantId_ReturnFromSubdomain_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("acme.example.com");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("acme");
	}

	[Fact]
	public void TenantId_SkipWwwSubdomain()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("www.example.com");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldBeNull();
	}

	[Fact]
	public void TenantId_SkipAppSubdomain()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("app.example.com");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldBeNull();
	}

	#endregion

	#region ETag Tests

	[Fact]
	public void ETag_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).ETag());
	}

	[Fact]
	public void ETag_ReturnNull_WhenNoETagHeaders()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var etag = httpContext.ETag();

		// Assert
		etag.ShouldBeNull();
	}

	[Fact]
	public void ETag_ReturnIfMatch_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[HeaderNames.IfMatch] = "\"etag-123\"";

		// Act
		var etag = httpContext.ETag();

		// Assert
		etag.ShouldBe("\"etag-123\"");
	}

	[Fact]
	public void ETag_ReturnIfNoneMatch_WhenPresent()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[HeaderNames.IfNoneMatch] = "\"etag-456\"";

		// Act
		var etag = httpContext.ETag();

		// Assert
		etag.ShouldBe("\"etag-456\"");
	}

	[Fact]
	public void ETag_PreferIfMatch_OverIfNoneMatch()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[HeaderNames.IfMatch] = "\"match-etag\"";
		httpContext.Request.Headers[HeaderNames.IfNoneMatch] = "\"none-match-etag\"";

		// Act
		var etag = httpContext.ETag();

		// Assert
		etag.ShouldBe("\"match-etag\"");
	}

	#endregion

	#region RemoteIpAddress Tests

	[Fact]
	public void RemoteIpAddress_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).RemoteIpAddress());
	}

	[Fact]
	public void RemoteIpAddress_ReturnNull_WhenNoConnection()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var ip = httpContext.RemoteIpAddress();

		// Assert — DefaultHttpContext has a Connection but no RemoteIpAddress
		ip.ShouldBeNull();
	}

	#endregion

	#region CreateDispatchMessageContext Tests

	[Fact]
	public void CreateDispatchMessageContext_ThrowWhenNotAuthenticated()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

		// Act & Assert
		Should.Throw<UnauthorizedAccessException>(() =>
			httpContext.CreateDispatchMessageContext());
	}

	[Fact]
	public void CreateDispatchMessageContext_ThrowWhenHttpContextIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HttpContext)null!).CreateDispatchMessageContext());
	}

	#endregion
}
