// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Depth tests for <see cref="HttpContextExtensions.CreateDispatchMessageContext"/>
/// covering the happy path and header propagation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HttpContextExtensionsDepthShould : UnitTestBase
{
	private static readonly IServiceProvider TestServiceProvider
		= new ServiceCollection().BuildServiceProvider();

	#region CreateDispatchMessageContext — Happy Path

	[Fact]
	public void CreateDispatchMessageContext_SetSourceToWebRequest()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.Source.ShouldBe("WebRequest");
	}

	[Fact]
	public void CreateDispatchMessageContext_SetUserIdFromNameIdentifierClaim()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext("user-42");

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.UserId.ShouldBe("user-42");
	}

	[Fact]
	public void CreateDispatchMessageContext_DefaultToAnonymous_WhenNoNameIdentifierClaim()
	{
		// Arrange — authenticated but without NameIdentifier
		var identity = new ClaimsIdentity("TestAuth");
		var httpContext = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(identity),
			RequestServices = TestServiceProvider,
		};
		httpContext.Request.Host = new HostString("localhost");

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.UserId.ShouldBe("anonymous");
	}

	[Fact]
	public void CreateDispatchMessageContext_GenerateCorrelationId_WhenNoHeader()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.CorrelationId.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(messageContext.CorrelationId, out _).ShouldBeTrue();
	}

	[Fact]
	public void CreateDispatchMessageContext_UseCorrelationIdFromHeader()
	{
		// Arrange
		var expected = Guid.NewGuid();
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Headers["X-Correlation-Id"] = expected.ToString();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.CorrelationId.ShouldBe(expected.ToString());
	}

	[Fact]
	public void CreateDispatchMessageContext_SetCausationId_WhenHeaderPresent()
	{
		// Arrange
		var causation = Guid.NewGuid();
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Headers["X-Causation-Id"] = causation.ToString();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.CausationId.ShouldBe(causation.ToString());
	}

	[Fact]
	public void CreateDispatchMessageContext_SetCausationIdToNull_WhenNoHeader()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.CausationId.ShouldBeNull();
	}

	[Fact]
	public void CreateDispatchMessageContext_SetTenantId_FromHeader()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Headers["X-Tenant-Id"] = "tenant-abc";

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void CreateDispatchMessageContext_SetTenantIdToNull_WhenNoTenantInfo()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Host = new HostString("localhost");

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.TenantId.ShouldBeNull();
	}

	[Fact]
	public void CreateDispatchMessageContext_PopulateItemsWithRequestHeaders()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Headers["X-Custom-Header"] = "custom-value";
		httpContext.Request.Headers["X-Another-Header"] = "another-value";

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.Items.ShouldContainKey("X-Custom-Header");
		messageContext.Items["X-Custom-Header"].ShouldBe("custom-value");
		messageContext.Items.ShouldContainKey("X-Another-Header");
		messageContext.Items["X-Another-Header"].ShouldBe("another-value");
	}

	[Fact]
	public void CreateDispatchMessageContext_SetRequestServices()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext();

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.RequestServices.ShouldNotBeNull();
		messageContext.RequestServices.ShouldBeSameAs(httpContext.RequestServices);
	}

	#endregion

	#region TenantId — Priority Ordering

	[Fact]
	public void TenantId_PreferHeader_OverRouteValue()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Tenant-Id"] = "header-tenant";
		httpContext.Request.RouteValues["tenantId"] = "route-tenant";

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("header-tenant");
	}

	[Fact]
	public void TenantId_PreferRouteValue_OverQueryString()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.RouteValues["tenantId"] = "route-tenant";
		httpContext.Request.QueryString = new QueryString("?tenantId=query-tenant");

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("route-tenant");
	}

	[Fact]
	public void TenantId_PreferQueryString_OverClaim()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("localhost");
		httpContext.Request.QueryString = new QueryString("?tenantId=query-tenant");
		httpContext.User = new ClaimsPrincipal(
			new ClaimsIdentity([new Claim("tenant_id", "claim-tenant")], "test"));

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("query-tenant");
	}

	[Fact]
	public void TenantId_PreferClaim_OverSubdomain()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("acme.example.com");
		httpContext.User = new ClaimsPrincipal(
			new ClaimsIdentity([new Claim("tenant_id", "claim-tenant")], "test"));

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe("claim-tenant");
	}

	#endregion

	#region RemoteIpAddress

	[Fact]
	public void RemoteIpAddress_ReturnIpAddress_WhenConnectionHasRemoteIp()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

		// Act
		var ip = httpContext.RemoteIpAddress();

		// Assert
		ip.ShouldBe("192.168.1.100");
	}

	#endregion

	#region Helpers

	private static DefaultHttpContext CreateAuthenticatedContext(string? userId = null)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.Name, "Test User"),
		};

		if (userId != null)
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
		}

		var identity = new ClaimsIdentity(claims, "TestAuth");
		var httpContext = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(identity),
			RequestServices = TestServiceProvider,
		};
		httpContext.Request.Host = new HostString("localhost");

		return httpContext;
	}

	#endregion
}
