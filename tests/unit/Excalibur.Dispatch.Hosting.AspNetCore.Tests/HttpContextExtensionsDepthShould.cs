// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.Dispatch.Features;
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
		messageContext.GetSource().ShouldBe("WebRequest");
	}

	[Fact]
	public void CreateDispatchMessageContext_SetUserIdFromNameIdentifierClaim()
	{
		// Arrange
		var httpContext = CreateAuthenticatedContext("user-42");

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.GetUserId().ShouldBe("user-42");
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
		messageContext.GetUserId().ShouldBe("anonymous");
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
		messageContext.GetTenantId().ShouldBe("tenant-abc");
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
		messageContext.GetTenantId().ShouldBeNull();
	}

	[Fact]
	public void CreateDispatchMessageContext_PopulateItemsWithAllowlistedRequestHeaders()
	{
		// This arm used to assert that EVERY request header landed in Items under its raw name. That was
		// the defect: a context item is put on the wire by every remote transport and persisted by one
		// outbox provider, so the caller's Authorization and Cookie went with it. The contract is now
		// opt-in by name, and a captured header carries the header. prefix.
		var httpContext = CreateAuthenticatedContext();
		httpContext.RequestServices = HeaderCaptureServices("X-Custom-Header", "X-Another-Header");
		httpContext.Request.Headers["X-Custom-Header"] = "custom-value";
		httpContext.Request.Headers["X-Another-Header"] = "another-value";

		var messageContext = httpContext.CreateDispatchMessageContext();

		messageContext.Items["header.X-Custom-Header"].ShouldBe("custom-value");
		messageContext.Items["header.X-Another-Header"].ShouldBe("another-value");
	}

	[Fact]
	public void CreateDispatchMessageContext_CaptureNoHeaders_WhenNothingIsAllowlisted()
	{
		// The safety half of the arm above, and the one that closes every downstream sink at the source:
		// with no opt-in, a credential-bearing request produces a context indistinguishable from one
		// built for a request that carried no headers at all.
		var httpContext = CreateAuthenticatedContext();
		httpContext.Request.Headers.Authorization = "Bearer a-caller-token";
		httpContext.Request.Headers.Cookie = "session=a-caller-session";
		httpContext.Request.Headers["X-Api-Key"] = "a-caller-api-key";

		var messageContext = httpContext.CreateDispatchMessageContext();

		messageContext.Items.Keys.ShouldNotContain(k => k.StartsWith("header.", StringComparison.Ordinal));
		messageContext.Items.Keys.ShouldNotContain("Authorization");
		messageContext.Items.Keys.ShouldNotContain("Cookie");
		messageContext.Items.Count.ShouldBe(
			CreateAuthenticatedContext().CreateDispatchMessageContext().Items.Count);
	}

	[Fact]
	public void CreateDispatchMessageContext_DropAnOverLongHeaderValue_RatherThanTruncateIt()
	{
		// A truncated value is still a value: it reads downstream as though it were whole, and nothing
		// there can tell that it is not.
		var httpContext = CreateAuthenticatedContext();
		httpContext.RequestServices = HeaderCaptureServices("X-Custom-Header");
		httpContext.Request.Headers["X-Custom-Header"] = new string('v', 257);

		var messageContext = httpContext.CreateDispatchMessageContext();

		messageContext.Items.ShouldNotContainKey("header.X-Custom-Header");
	}

	private static IServiceProvider HeaderCaptureServices(params string[] allowed)
	{
		var services = new ServiceCollection();
		_ = services.AddDispatchRequestHeaderCapture(options =>
		{
			foreach (var name in allowed)
			{
				_ = options.AllowedHeaders.Add(name);
			}
		});
		return services.BuildServiceProvider();
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
		tenantId.ShouldBe("header-tenant");
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
		tenantId.ShouldBe("route-tenant");
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
		tenantId.ShouldBe("query-tenant");
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
		tenantId.ShouldBe("claim-tenant");
	}

	#endregion

	#region CreateDispatchMessageContext — Anonymous / Unauthenticated

	[Fact]
	public void CreateDispatchMessageContext_SetAnonymousUserId_WhenUserIsNotAuthenticated()
	{
		// Arrange — explicitly unauthenticated identity (no auth type)
		var httpContext = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity()),
			RequestServices = TestServiceProvider,
		};

		// Act
		var messageContext = httpContext.CreateDispatchMessageContext();

		// Assert
		messageContext.GetUserId().ShouldBe("anonymous");
	}

	#endregion

	#region CorrelationId — Uniqueness

	[Fact]
	public void CorrelationId_GenerateDifferentValues_AcrossMultipleCalls()
	{
		// Arrange
		var httpContext1 = new DefaultHttpContext();
		var httpContext2 = new DefaultHttpContext();

		// Act
		var id1 = httpContext1.CorrelationId();
		var id2 = httpContext2.CorrelationId();

		// Assert — each call without a header should produce a unique ID
		id1.Value.ShouldNotBe(id2.Value);
	}

	#endregion

	#region CausationId — Edge Cases

	[Fact]
	public void CausationId_ReturnNull_WhenHeaderIsEmptyString()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Causation-Id"] = "";

		// Act
		var causationId = httpContext.CausationId();

		// Assert
		causationId.ShouldBeNull();
	}

	#endregion

	#region TenantId — Edge Cases

	[Fact]
	public void TenantId_ReturnNull_WhenClaimIsWhitespaceOnly()
	{
		// Arrange — whitespace-only claim should be treated as absent
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("localhost");
		httpContext.User = new ClaimsPrincipal(
			new ClaimsIdentity([new Claim("tenant_id", "   ")], "test"));

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldBeNull();
	}

	[Fact]
	public void TenantId_ReturnNull_WhenClaimIsEmptyString()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Host = new HostString("localhost");
		httpContext.User = new ClaimsPrincipal(
			new ClaimsIdentity([new Claim("tenant_id", "")], "test"));

		// Act
		var tenantId = httpContext.TenantId();

		// Assert
		tenantId.ShouldBeNull();
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

	[Fact]
	public void RemoteIpAddress_ReturnIpv6Address_WhenConnectionHasIpv6()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.IPv6Loopback;

		// Act
		var ip = httpContext.RemoteIpAddress();

		// Assert
		ip.ShouldBe("::1");
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
