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
/// Security lock for the W3 dashboard mutating-action boundary (`4e42zy`). The design guarantee is
/// <strong>structurally read-only by default</strong>: with <c>DashboardOptions.EnableMutatingActions</c>
/// false (the default) the mutating endpoint group is never mapped, so a state-changing request has
/// <em>zero attack surface</em> (404, not merely auth-gated); with it enabled the group is mapped AND
/// carries native ASP.NET Core <c>RequireAuthorization()</c>.
/// </summary>
/// <remarks>
/// Drives the real committed <c>MapDashboardApi</c> wiring through an in-process ASP.NET Core
/// <see cref="TestServer"/> against the real <c>DeadLetterReplayMutatingModule</c> (<c>POST
/// /dashboard/api/dlq/{id}/replay</c>). Non-vacuous per the AC: disabled ⇒ 404; enabled + unauthenticated
/// ⇒ 401; enabled + authenticated ⇒ the auth boundary is passed and the request reaches the handler
/// (never 401/403). Never skipped, no Docker (in-process feature).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class DashboardAuthBoundaryShould
{
	private const string TestScheme = "Test";

	private static Uri ReplayPath() => new($"/dashboard/api/dlq/{Guid.NewGuid()}/replay", UriKind.Relative);

	// No-auth host (mutating group not mapped when disabled → auth is irrelevant).
	private static async Task<WebApplication> BuildHostAsync(bool enableMutating)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.EnableMutatingActions = enableMutating);

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	// Auth-enabled host with a test authentication scheme backed by the given handler.
	private static async Task<WebApplication> BuildHostAsync<THandler>(bool enableMutating)
		where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.EnableMutatingActions = enableMutating);
		builder.Services
			.AddAuthentication(TestScheme)
			.AddScheme<AuthenticationSchemeOptions, THandler>(TestScheme, configureOptions: null);
		builder.Services.AddAuthorization();

		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task Return404ForAMutatingRequestWhenMutatingActionsAreDisabled()
	{
		// Default: EnableMutatingActions = false → the mutating group is never mapped → zero attack surface.
		await using var app = await BuildHostAsync(enableMutating: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.PostAsync(ReplayPath(), content: null).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Return401ForAMutatingRequestWhenEnabledButUnauthenticated()
	{
		await using var app = await BuildHostAsync<RejectingAuthHandler>(enableMutating: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.PostAsync(ReplayPath(), content: null).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ReachTheHandlerWhenEnabledAndAuthenticated()
	{
		// An authenticated caller passes RequireAuthorization → the request reaches the mutating handler
		// (which fail-opens with no dead-letter queue configured). The key guarantee: it is NOT blocked
		// by the auth boundary (never 401/403), and the endpoint exists (not the disabled 404).
		await using var app = await BuildHostAsync<AcceptingAuthHandler>(enableMutating: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.PostAsync(ReplayPath(), content: null).ConfigureAwait(false);

		response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
		response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
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

	/// <summary>Authenticates every request as a fixed principal → passes the default authorization policy.</summary>
	private sealed class AcceptingAuthHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			var identity = new ClaimsIdentity(
				[new Claim(ClaimTypes.Name, "dashboard-operator")], TestScheme);
			var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}
