// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;
using System.Text.Encodings.Web;

using Excalibur.A3.AspNetCore;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using A3PolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace GrantAuthorizedApiSample;

/// <summary>
/// Drives the sample: issues the requests below against the running host and prints the status code each
/// one produced, so the behaviour is visible without a separate client.
/// </summary>
public static class DemoRunner
{
	public static async Task RunAsync(WebApplication app)
	{
		// The sample calls ITSELF, which makes the choice of endpoint a correctness question rather
		// than a preference. app.Urls lists the HTTPS endpoint first, and the ASP.NET development
		// certificate can be GENERATED on any platform but only TRUSTED on Windows and macOS --
		// `dotnet dev-certs https --trust` is a no-op elsewhere. So on Linux the host binds happily
		// and this client then rejects its own server with UntrustedRoot, which surfaces as an
		// unhandled exception and aborts the process.
		//
		// Every other web sample only LISTENS, so none of them touches the trust store and none of
		// them showed the problem. Prefer the plain-HTTP endpoint for the loopback call: it needs no
		// certificate anywhere, and the HTTPS endpoint stays bound for anyone browsing the sample.
		var address = app.Urls.FirstOrDefault(
				static url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
			?? app.Urls.First();

		using var client = new HttpClient { BaseAddress = new Uri(address) };

		Console.WriteLine($"Listening on {address}");
		Console.WriteLine();
		Console.WriteLine("  grants: alice -> order-1, order-2   bob -> order-1     (all in tenant-a)");
		Console.WriteLine();

		foreach (var style in new[] { "/orders", "/mvc/orders" })
		{
			Console.WriteLine(style == "/orders" ? "Minimal API" : "MVC controller");

			await ShowAsync(client, $"{style}/order-2", "alice", "tenant-a", "holds the grant");
			await ShowAsync(client, $"{style}/order-2", "bob", "tenant-a", "holds Read on Order, but not on THIS order");
			await ShowAsync(client, $"{style}/order-1", "alice", "tenant-b", "holds the grant, but in another tenant");
			await ShowAsync(client, $"{style}/order-1", user: null, tenant: null, "not authenticated");

			Console.WriteLine();
		}
	}

	private static async Task ShowAsync(HttpClient client, string path, string? user, string? tenant, string note)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);

		if (user is not null)
		{
			request.Headers.Add(DemoAuthenticationHandler.UserHeader, user);
		}

		if (tenant is not null)
		{
			request.Headers.Add(DemoAuthenticationHandler.TenantHeader, tenant);
		}

		using var response = await client.SendAsync(request);

		Console.WriteLine($"  {(int)response.StatusCode} {response.StatusCode,-12} {path,-24} as {user ?? "anonymous",-10} — {note}");
	}
}

/// <summary>An MVC controller gated by the same grant, using the same strongly-typed attribute.</summary>
[ApiController]
public sealed class OrdersController : ControllerBase
{
	[HttpGet("/mvc/orders/{id}")]
	[RequireGrant("Read", "Order", "id")]
	public IActionResult GetById(string id) => Ok($"mvc order {id}");
}

/// <summary>
/// Demo authentication: reads the caller from request headers so the sample runs with no identity
/// provider. Replace with a real scheme.
/// </summary>
public sealed class DemoAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "Demo";
	public const string UserHeader = "X-Demo-User";
	public const string TenantHeader = "X-Demo-Tenant";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrEmpty(user))
		{
			return Task.FromResult(AuthenticateResult.NoResult());
		}

		var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()) };

		if (Request.Headers.TryGetValue(TenantHeader, out var tenant) && !string.IsNullOrEmpty(tenant))
		{
			claims.Add(new Claim("tenant_id", tenant.ToString()));
		}

		return Task.FromResult(AuthenticateResult.Success(
			new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
	}
}

/// <summary>An in-memory grant set, standing in for a grant store.</summary>
public sealed class DemoGrants(HashSet<string> entries)
{
	public bool IsGranted(string user, string tenant, string activity, string? resourceId) =>
		entries.Contains($"{user}|{tenant}|{activity}|{resourceId}");
}

/// <summary>
/// Supplies the caller's grants. The user and tenant arrive through the bridge that
/// <c>AddHttpGrantAuthorization</c> registered, so they reflect the authenticated request.
/// </summary>
public sealed class DemoGrantPolicyProvider(
	IAuthenticationToken token,
	ITenantContext tenantContext,
	DemoGrants grants) : A3PolicyProvider
{
	public Task<IAuthorizationPolicy> GetPolicyAsync()
	{
		// The same preconditions the framework's provider applies: an unresolved caller or tenant is a
		// denial, never an evaluation against nothing.
		if (token.UserId is null)
		{
			throw new InvalidOperationException("User ID is required for authorization policy.");
		}

		if (string.IsNullOrEmpty(tenantContext.TenantId))
		{
			throw new InvalidOperationException("Tenant ID is required for authorization policy.");
		}

		return Task.FromResult<IAuthorizationPolicy>(
			new DemoPolicy(token.UserId, tenantContext.TenantId, grants));
	}
}

/// <summary>The caller's grants for one request.</summary>
public sealed class DemoPolicy(string userId, string tenantId, DemoGrants grants) : IAuthorizationPolicy
{
	// UserId is nullable on the interface but is always supplied here, so the lookup reads this
	// non-nullable copy. Every constructor parameter below is used ONLY to initialize state, never
	// captured into a method body as well -- doing both is what the compiler rejects.
	private readonly string _userId = userId;

	public string TenantId { get; } = tenantId;

	public string? UserId { get; } = userId;

	public bool IsAuthorized(string activityName, string? resourceId) =>
		grants.IsGranted(_userId, TenantId, activityName, resourceId);

	public bool HasGrant(string activityName) => IsAuthorized(activityName, null);

	public bool HasGrant<TActivity>() => HasGrant(typeof(TActivity).Name);

	public bool HasGrant(string resourceType, string resourceId) => IsAuthorized(resourceType, resourceId);

	public bool HasGrant<TResourceType>(string resourceId) => HasGrant(typeof(TResourceType).Name, resourceId);
}
