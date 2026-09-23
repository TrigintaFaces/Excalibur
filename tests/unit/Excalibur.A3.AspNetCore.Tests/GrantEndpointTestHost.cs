// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;
using System.Text.Encodings.Web;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// Boots a real ASP.NET Core host on a loopback port so the tests exercise the genuine authentication,
/// routing and authorization middleware rather than a substitute pipeline. The status codes asserted are
/// the ones a consumer would observe.
/// </summary>
internal sealed class GrantEndpointTestHost : IAsyncDisposable
{
	public const string SchemeName = "Test";
	public const string UserHeader = "X-Test-User";
	public const string TenantHeader = "X-Test-Tenant";

	private readonly WebApplication _app;

	private GrantEndpointTestHost(WebApplication app, HttpClient client)
	{
		_app = app;
		Client = client;
	}

	public HttpClient Client { get; }

	/// <summary>
	/// Starts a host whose grant store is <paramref name="grants"/>, a set of
	/// <c>userId|tenantId|activity|resourceId</c> tuples. Absence from the set is a denial.
	/// </summary>
	public static async Task<GrantEndpointTestHost> StartAsync(
		TestGrantStore grants,
		Action<WebApplication> configureEndpoints,
		bool addControllers = false,
		Action<GrantAuthorizationHttpOptions>? configureOptions = null,
		Action<IServiceCollection>? configureServices = null)
	{
		var builder = WebApplication.CreateSlimBuilder();

		builder.WebHost.UseUrls("http://127.0.0.1:0");
		builder.Logging.ClearProviders();

		_ = builder.Services.AddSingleton(grants);

		// The grant lookup is substituted; the identity and tenant it reads are NOT — this provider
		// resolves them from the same DI contracts the real A3 provider does, and reproduces the real
		// provider's precondition that a missing user or tenant is an InvalidOperationException.
		_ = builder.Services.AddScoped<IAuthorizationPolicyProvider, TestA3PolicyProvider>();

		_ = builder.Services
			.AddAuthentication(SchemeName)
			.AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(SchemeName, static _ => { });

		if (addControllers)
		{
			_ = builder.Services.AddControllers()
				.AddApplicationPart(typeof(GrantEndpointTestHost).Assembly);
		}

		_ = builder.Services.AddHttpGrantAuthorization(configureOptions ?? (static _ => { }));

		configureServices?.Invoke(builder.Services);

		var app = builder.Build();

		app.UseAuthentication();
		app.UseAuthorization();

		configureEndpoints(app);

		if (addControllers)
		{
			_ = app.MapControllers();
		}

		await app.StartAsync().ConfigureAwait(false);

		var address = app.Services
			.GetRequiredService<IServer>()
			.Features
			.Get<IServerAddressesFeature>()!
			.Addresses
			.First();

		var client = new HttpClient { BaseAddress = new Uri(address) };

		return new GrantEndpointTestHost(app, client);
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Issues a request as <paramref name="userId"/> in <paramref name="tenantId"/>. A null
	/// <paramref name="userId"/> is an anonymous caller.
	/// </summary>
	public async Task<HttpResponseMessage> GetAsync(string path, string? userId, string? tenantId = "tenant-1")
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);

		if (userId is not null)
		{
			request.Headers.Add(UserHeader, userId);
		}

		if (tenantId is not null)
		{
			request.Headers.Add(TenantHeader, tenantId);
		}

		return await Client.SendAsync(request).ConfigureAwait(false);
	}

	private sealed class HeaderAuthenticationHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			if (!Request.Headers.TryGetValue(UserHeader, out var userId) || string.IsNullOrEmpty(userId))
			{
				return Task.FromResult(AuthenticateResult.NoResult());
			}

			var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };

			if (Request.Headers.TryGetValue(TenantHeader, out var tenantId) && !string.IsNullOrEmpty(tenantId))
			{
				claims.Add(new Claim("tenant_id", tenantId.ToString()));
			}

			var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

			return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
		}
	}

	private sealed class TestA3PolicyProvider(
		IAuthenticationToken token,
		ITenantContext tenantContext,
		TestGrantStore grants) : IAuthorizationPolicyProvider
	{
		public Task<IAuthorizationPolicy> GetPolicyAsync()
		{
			if (token.UserId is null)
			{
				throw new InvalidOperationException("User ID is required for authorization policy.");
			}

			if (string.IsNullOrEmpty(tenantContext.TenantId))
			{
				throw new InvalidOperationException("Tenant ID is required for authorization policy.");
			}

			return Task.FromResult<IAuthorizationPolicy>(
				new TestAuthorizationPolicy(token.UserId, tenantContext.TenantId, grants));
		}
	}
}
