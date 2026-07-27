// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Endpoint lock for the W1-12 provider-capability graceful-degradation guarantee (`bx0cxh`): a subsystem
/// whose backing store is <em>not registered</em> must fail open — its read endpoint returns
/// <c>200 OK</c> with a <c>{ configured: false }</c> payload, mirroring <c>IDistributedCache</c> skip
/// semantics, and <strong>never a 500</strong>.
/// </summary>
/// <remarks>
/// Author≠impl (TestsDeveloper): drives the <em>real committed</em> read modules end-to-end through an
/// in-process ASP.NET Core <see cref="TestServer"/> with <strong>no</strong> admin/store services
/// registered, so every subsystem exercises its optional-service (<c>GetService</c>-probe) fail-open
/// branch. Non-skipped, no Docker — the feature is in-process. Every mapped read endpoint is enumerated
/// (outbox / inbox / saga / dlq / leader / projection-lag), not sampled, so a regression that lets any one
/// subsystem throw on an absent store is caught here rather than at the full-CI backstop.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class DashboardReadEndpointFailOpenShould
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildBareHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();

		// Base dashboard only — NO outbox/inbox/saga/dlq/leader store registered → every read module
		// must fail open. The projection-lag module lives in a sibling package and is opt-in.
		builder.Services.AddDashboard();
		builder.Services.AddProjectionLagDashboard(); // maps /projections/lag; no IProjectionLagReadModel registered

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	// The camelCase "configured" flag is shared by every not-configured read view (OutboxView, InboxView,
	// SagaView, DeadLetterView, LeaderView, ProjectionLagView); a single probe DTO reads them all.
	private sealed record NotConfiguredProbe(bool Configured);

	public static TheoryData<string> ReadEndpointsThatReportConfigured() =>
	[
		"/dashboard/api/outbox",
		"/dashboard/api/inbox",
		"/dashboard/api/saga",
		"/dashboard/api/dlq",
		"/dashboard/api/leader",
		"/dashboard/api/projections/lag",
	];

	[Theory]
	[MemberData(nameof(ReadEndpointsThatReportConfigured))]
	public async Task Return200WithConfiguredFalseWhenTheBackingStoreIsAbsent(string path)
	{
		await using var app = await BuildBareHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);

		// Fail-open: never a 500, always a well-formed not-configured payload.
		response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{path} must fail open, not 500");

		var probe = await response.Content.ReadFromJsonAsync<NotConfiguredProbe>(JsonOptions).ConfigureAwait(false);
		probe.ShouldNotBeNull();
		probe.Configured.ShouldBeFalse($"{path} must report configured:false when its store is absent");
	}

	[Fact]
	public async Task ReturnAnEmptyPageForDlqEntriesWhenNoQueueIsConfigured()
	{
		// The paged /dlq/entries endpoint fails open to an empty array rather than a not-configured object.
		await using var app = await BuildBareHostAsync().ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client
			.GetAsync(new Uri("/dashboard/api/dlq/entries", UriKind.Relative)).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var entries = await response.Content
			.ReadFromJsonAsync<JsonElement[]>(JsonOptions).ConfigureAwait(false);
		entries.ShouldNotBeNull();
		entries.ShouldBeEmpty();
	}
}
