// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Excalibur.EventSourcing.Projections;
using Excalibur.Operations.Dashboard.EventSourcing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Endpoint lock for the W1-10 projection/CDC-lag read endpoint <c>GET /dashboard/api/projections/lag</c>
/// (`8gj5m3`). Author≠impl (TestsDeveloper): drives the <em>real committed</em>
/// <c>ProjectionLagDashboardModule</c> end-to-end through an in-process ASP.NET Core
/// <see cref="TestServer"/> against a real (non-mock) <see cref="IProjectionLagReadModel"/>, asserting the
/// emitted <c>ProjectionLagView</c> contract: per-stream <c>lag = max(0, head - checkpoint)</c> mapping.
/// </summary>
/// <remarks>
/// The absent-read-model fail-open path (<c>configured:false</c>, never 500) is locked by
/// <c>DashboardReadEndpointFailOpenShould</c>; this file locks the <strong>configured</strong> path.
/// Non-skipped, no Docker — the feature is the composed in-process minimal-API host.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class ProjectionLagEndpointShould
{
	private const string LagPath = "/dashboard/api/projections/lag";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildHostAsync(IProjectionLagReadModel? readModel)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard();
		builder.Services.AddProjectionLagDashboard();
		if (readModel is not null)
		{
			builder.Services.AddSingleton(readModel);
		}

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task ReportPerStreamLagWhenAReadModelIsConfigured()
	{
		var readModel = new SeededLagReadModel(
			new ProjectionLag("orders-projection", CheckpointPosition: 90, HeadPosition: 100, Lag: 10),
			new ProjectionLag("audit-projection", CheckpointPosition: 100, HeadPosition: 100, Lag: 0));
		await using var app = await BuildHostAsync(readModel).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var view = await client.GetFromJsonAsync<ProjectionLagView>(LagPath, JsonOptions).ConfigureAwait(false);

		view.ShouldNotBeNull();
		view.Configured.ShouldBeTrue();
		view.Streams.Count.ShouldBe(2);

		var orders = view.Streams.Single(s => s.SubscriptionName == "orders-projection");
		orders.CheckpointPosition.ShouldBe(90);
		orders.HeadPosition.ShouldBe(100);
		orders.Lag.ShouldBe(10);

		var audit = view.Streams.Single(s => s.SubscriptionName == "audit-projection");
		audit.Lag.ShouldBe(0);
	}

	[Fact]
	public async Task ReportConfiguredWithNoStreamsWhenNoCheckpointsExist()
	{
		await using var app = await BuildHostAsync(new SeededLagReadModel()).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var view = await client.GetFromJsonAsync<ProjectionLagView>(LagPath, JsonOptions).ConfigureAwait(false);

		view.ShouldNotBeNull();
		view.Configured.ShouldBeTrue();
		view.Streams.ShouldBeEmpty();
	}

	/// <summary>A real (non-mock) <see cref="IProjectionLagReadModel"/> seeded with a fixed lag set.</summary>
	private sealed class SeededLagReadModel(params ProjectionLag[] seed) : IProjectionLagReadModel
	{
		private readonly IReadOnlyList<ProjectionLag> _lag = [.. seed];

		public ValueTask<IReadOnlyList<ProjectionLag>> GetLagAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(_lag);
	}
}
