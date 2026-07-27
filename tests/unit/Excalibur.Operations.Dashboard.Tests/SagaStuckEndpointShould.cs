// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Endpoint lock for the <c>GET /dashboard/api/saga/stuck</c> read-model (W1-6 stretch, `ci5iht`
/// completion). Author≠impl (TestsDeveloper): drives the <em>real committed handler</em>
/// (<c>SagaDashboardModule.GetStuckAsync</c>) end-to-end through a real in-process ASP.NET Core
/// <see cref="TestServer"/> host + real DI/routing + the real in-memory saga store, asserting the emitted
/// HTTP/JSON contract.
/// </summary>
/// <remarks>
/// <para>
/// Non-skipped, no Docker — the feature is in-process, so the real infrastructure is the composed minimal-API
/// host. Coverage: a <strong>due</strong> timeout for a running saga surfaces; a <strong>not-due</strong>
/// timeout is absent; a saga that <strong>completed</strong> since its timeout came due is excluded; and the
/// capability-gated <strong>fail-open</strong> path (no <see cref="ISagaTimeoutStore"/> registered) returns
/// <c>{ available: false }</c> — never a 500.
/// </para>
/// <para>
/// The timeout source is a real (non-mock) <see cref="ISagaTimeoutStore"/> implementation rather than the
/// production <c>InMemorySagaTimeoutStore</c>: that type is <c>internal</c> to <c>Excalibur.Saga</c> (no
/// friend access here) and is only registered together with the <c>SagaTimeoutDeliveryService</c> hosted
/// poller, which starts on <see cref="WebApplication.StartAsync"/> and would consume the seeded due timeout
/// on its immediate first poll (a race that would make the lock flaky/vacuous). The <see cref="ISagaStore"/>
/// / <see cref="ISagaStoreAdmin"/> used for the completed-exclusion is the real production in-memory store.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class SagaStuckEndpointShould
{
	private const string StuckPath = "/dashboard/api/saga/stuck";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildHostAsync(ISagaTimeoutStore? timeoutStore)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();

		builder.Services.AddDashboard();          // registers SagaDashboardModule + validated DashboardOptions
		builder.Services.AddInMemorySagaStore();  // real ISagaStore + ISagaStoreAdmin (same instance)
		if (timeoutStore is not null)
		{
			builder.Services.AddSingleton(timeoutStore); // real timeout store, NO hosted delivery poller
		}

		var app = builder.Build();
		app.MapDashboardApi();                     // maps /dashboard/api/... incl. /saga/stuck
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	private static async Task SeedSagaAsync(WebApplication app, Guid sagaId, bool completed)
	{
		var store = app.Services.GetRequiredService<ISagaStore>();
		await store.SaveAsync(
			new StuckLockSagaState
			{
				SagaId = sagaId,
				Completed = completed,
				CompletedAt = completed ? DateTimeOffset.UtcNow : null,
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	private static SagaTimeout DueTimeout(Guid sagaId, DateTimeOffset now) => new(
		TimeoutId: "timeout-1",
		SagaId: sagaId.ToString(),
		SagaType: "OrderSaga",
		TimeoutType: "OrderTimeout",
		TimeoutData: null,
		DueAt: now.AddSeconds(-30),
		ScheduledAt: now.AddMinutes(-5));

	[Fact]
	public async Task SurfaceADueTimeoutForARunningSaga()
	{
		var sagaId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		await using var app = await BuildHostAsync(new SeededTimeoutStore(DueTimeout(sagaId, now))).ConfigureAwait(false);
		await SeedSagaAsync(app, sagaId, completed: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var result = await client.GetFromJsonAsync<StuckSagaResult>(StuckPath, JsonOptions).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.Available.ShouldBeTrue();
		result.Stuck.ShouldHaveSingleItem();
		result.Stuck[0].SagaId.ShouldBe(sagaId.ToString());
		result.Stuck[0].TimeoutId.ShouldBe("timeout-1");
		result.Stuck[0].OverdueSeconds.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExcludeATimeoutThatIsNotYetDue()
	{
		var sagaId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var notDue = new SagaTimeout("timeout-future", sagaId.ToString(), "OrderSaga", "OrderTimeout",
			null, now.AddHours(1), now);
		await using var app = await BuildHostAsync(new SeededTimeoutStore(notDue)).ConfigureAwait(false);
		await SeedSagaAsync(app, sagaId, completed: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var result = await client.GetFromJsonAsync<StuckSagaResult>(StuckPath, JsonOptions).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.Available.ShouldBeTrue();
		result.Stuck.ShouldBeEmpty();
	}

	[Fact]
	public async Task ExcludeASagaThatCompletedSinceTheTimeoutCameDue()
	{
		var sagaId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		await using var app = await BuildHostAsync(new SeededTimeoutStore(DueTimeout(sagaId, now))).ConfigureAwait(false);
		await SeedSagaAsync(app, sagaId, completed: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var result = await client.GetFromJsonAsync<StuckSagaResult>(StuckPath, JsonOptions).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.Available.ShouldBeTrue();
		result.Stuck.ShouldBeEmpty();
	}

	[Fact]
	public async Task FailOpenWithAvailableFalseWhenNoTimeoutStoreIsRegistered()
	{
		await using var app = await BuildHostAsync(timeoutStore: null).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client.GetAsync(new Uri(StuckPath, UriKind.Relative)).ConfigureAwait(false);
		response.StatusCode.ShouldBe(HttpStatusCode.OK); // fail-open, never 500

		var result = await response.Content.ReadFromJsonAsync<StuckSagaResult>(JsonOptions).ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Available.ShouldBeFalse();
		result.Stuck.ShouldBeEmpty();
	}

	/// <summary>A real (non-mock) <see cref="ISagaTimeoutStore"/> seeded with a fixed set of timeouts.</summary>
	private sealed class SeededTimeoutStore(params SagaTimeout[] seed) : ISagaTimeoutStore
	{
		private readonly List<SagaTimeout> _timeouts = [.. seed];

		public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SagaTimeout>>(
				_timeouts.Where(t => t.DueAt <= asOf).OrderBy(t => t.DueAt).ToList());

		/// <summary>
		/// Leases the due timeouts, honoring the interface contract that a claimed timeout is not returned to a
		/// subsequent claimer. Modeled as remove-on-claim so a second call cannot re-deliver the same timeout —
		/// a stub that merely returned the due set would let a first-writer-wins lock pass vacuously.
		/// </summary>
		public Task<IReadOnlyList<SagaTimeout>> ClaimDueTimeoutsAsync(
			DateTimeOffset asOf,
			int batchSize,
			CancellationToken cancellationToken)
		{
			var claimed = _timeouts
				.Where(t => t.DueAt <= asOf)
				.OrderBy(t => t.DueAt)
				.Take(batchSize)
				.ToList();

			foreach (var timeout in claimed)
			{
				_ = _timeouts.Remove(timeout);
			}

			return Task.FromResult<IReadOnlyList<SagaTimeout>>(claimed);
		}

		public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
		{
			_timeouts.Add(timeout);
			return Task.CompletedTask;
		}

		public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task MarkDeliveredAsync(string timeoutId, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class StuckLockSagaState : SagaState
	{
	}
}
