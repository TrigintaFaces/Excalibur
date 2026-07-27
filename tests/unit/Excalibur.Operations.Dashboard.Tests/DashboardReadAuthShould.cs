// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Security lock for the optional read-side authorization gate (<c>DashboardOptions.ReadActionsPolicy</c>).
/// The dashboard is read-only-open by default; setting a read policy makes every read endpoint require an
/// authorized caller, symmetric with the mutating group.
/// </summary>
/// <remarks>
/// Drives the real committed <c>MapDashboardApi</c> read group (<c>GET /dashboard/api/</c>) through an
/// in-process ASP.NET Core <see cref="TestServer"/>. Non-vacuous: with no policy an unauthenticated read
/// succeeds (200 — open); with a policy set, an unauthenticated read is challenged (401); an authenticated
/// caller passes the boundary and reaches the handler (200, never 401/403). Never skipped, no Docker.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class DashboardReadAuthShould
{
	private const string TestScheme = "Test";
	private const string ReadPolicy = "DashboardRead";

	private static Uri ReadRoot() => new("/dashboard/api/", UriKind.Relative);

	// Open host: no ReadActionsPolicy, no authentication configured.
	private static async Task<WebApplication> BuildOpenHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard();

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	// Read-gated host: ReadActionsPolicy set to a policy requiring an authenticated user.
	private static async Task<WebApplication> BuildGatedHostAsync<THandler>()
		where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.ReadActionsPolicy = ReadPolicy);
		builder.Services
			.AddAuthentication(TestScheme)
			.AddScheme<AuthenticationSchemeOptions, THandler>(TestScheme, configureOptions: null);
		builder.Services.AddAuthorizationBuilder()
			.AddPolicy(ReadPolicy, p => p.RequireAuthenticatedUser());

		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task AllowAnUnauthenticatedReadWhenNoReadPolicyIsSet()
	{
		await using var app = await BuildOpenHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.GetAsync(ReadRoot()).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Return401ForAnUnauthenticatedReadWhenAReadPolicyIsSet()
	{
		await using var app = await BuildGatedHostAsync<RejectingAuthHandler>().ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.GetAsync(ReadRoot()).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ReachTheReadHandlerWhenAuthenticatedUnderAReadPolicy()
	{
		await using var app = await BuildGatedHostAsync<AcceptingAuthHandler>().ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.GetAsync(ReadRoot()).ConfigureAwait(false);

		response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
		response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	/// <summary>Rejects every request → an unauthenticated caller, forcing a 401 challenge.</summary>
	private sealed class RejectingAuthHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
			Task.FromResult(AuthenticateResult.NoResult());
	}

	/// <summary>Authenticates every request as a fixed principal → passes the read policy.</summary>
	private sealed class AcceptingAuthHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			var identity = new ClaimsIdentity(
				[new Claim(ClaimTypes.Name, "dashboard-reader")], TestScheme);
			var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}
