// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Excalibur.Dispatch.ErrorHandling;

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
/// Effect lock for the W3-2 dead-letter replay mutating endpoints (`k3lluy`): an authorized
/// <c>POST /dashboard/api/dlq/{id}/replay</c> (and <c>/dlq/replay-batch</c>) must actually drive the real
/// committed <c>DeadLetterReplayMutatingModule</c> into the underlying <c>IDeadLetterQueue.ReplayAsync</c> /
/// <c>IDeadLetterQueueAdmin.ReplayBatchAsync</c> — the message is re-dispatched — and surface the emitted
/// <c>DeadLetterReplayResult</c>.
/// </summary>
/// <remarks>
/// Author≠impl (TestsDeveloper). Complements <c>DashboardAuthBoundaryShould</c> (which locks the auth
/// boundary) by asserting the <em>effect past the boundary</em>: with mutating actions enabled and an
/// authenticated caller, the handler invokes replay against a real (non-mock) recording queue and the
/// replayed id/filter is observed on that queue — proving re-dispatch actually happened, not merely that a
/// 200 was returned. An unknown id fails open to 404. Non-skipped, no Docker (in-process feature).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class DeadLetterReplayEffectShould
{
	private const string TestScheme = "Test";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildAuthedHostAsync(RecordingDeadLetterQueue queue)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.EnableMutatingActions = true);
		builder.Services.AddSingleton<IDeadLetterQueue>(queue);
		builder.Services.AddSingleton<IDeadLetterQueueAdmin>(queue);
		builder.Services
			.AddAuthentication(TestScheme)
			.AddScheme<AuthenticationSchemeOptions, AcceptingAuthHandler>(TestScheme, configureOptions: null);
		builder.Services.AddAuthorization();

		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task ReplayAKnownEntryAndReDispatchItThroughTheRealQueue()
	{
		var id = Guid.NewGuid();
		var queue = new RecordingDeadLetterQueue(id);
		await using var app = await BuildAuthedHostAsync(queue).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client
			.PostAsync(new Uri($"/dashboard/api/dlq/{id}/replay", UriKind.Relative), content: null)
			.ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content
			.ReadFromJsonAsync<DeadLetterReplayResult>(JsonOptions).ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Replayed.ShouldBeTrue();
		result.Count.ShouldBe(1);

		// The emitted effect: the handler actually invoked ReplayAsync with the requested id (re-dispatch),
		// not just returned a 200 — observed on the real recording queue.
		queue.ReplayedIds.ShouldHaveSingleItem().ShouldBe(id);
	}

	[Fact]
	public async Task Return404WhenTheEntryToReplayIsNotFound()
	{
		var known = Guid.NewGuid();
		var queue = new RecordingDeadLetterQueue(known);
		await using var app = await BuildAuthedHostAsync(queue).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var missing = Guid.NewGuid();
		var response = await client
			.PostAsync(new Uri($"/dashboard/api/dlq/{missing}/replay", UriKind.Relative), content: null)
			.ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		// The queue was consulted (replay attempted) but no re-dispatch recorded for a missing id.
		queue.ReplayedIds.ShouldBeEmpty();
	}

	[Fact]
	public async Task BatchReplayReDispatchesEveryMatchingEntry()
	{
		var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var queue = new RecordingDeadLetterQueue(ids);
		await using var app = await BuildAuthedHostAsync(queue).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client
			.PostAsJsonAsync(
				new Uri("/dashboard/api/dlq/replay-batch", UriKind.Relative),
				new DeadLetterReplayBatchRequest(),
				JsonOptions)
			.ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content
			.ReadFromJsonAsync<DeadLetterReplayResult>(JsonOptions).ConfigureAwait(false);
		result.ShouldNotBeNull();
		result.Replayed.ShouldBeTrue();
		result.Count.ShouldBe(ids.Length);
		queue.BatchReplayCount.ShouldBe(1);
	}

	/// <summary>
	/// A real (non-mock) in-memory <see cref="IDeadLetterQueue"/> + <see cref="IDeadLetterQueueAdmin"/> that
	/// records which entries were re-dispatched, so a test can assert the replay <em>effect</em>, not a stub
	/// return value. Seeded with the ids that "exist" in the queue.
	/// </summary>
	private sealed class RecordingDeadLetterQueue(params Guid[] existing)
		: IDeadLetterQueue, IDeadLetterQueueAdmin
	{
		private readonly HashSet<Guid> _existing = [.. existing];

		public ConcurrentBag<Guid> ReplayedIds { get; } = [];

		public int BatchReplayCount { get; private set; }

		public Task<Guid> EnqueueAsync<T>(
			T message,
			DeadLetterReason reason,
			CancellationToken cancellationToken,
			Exception? exception = null,
			IDictionary<string, string>? metadata = null) => Task.FromResult(Guid.NewGuid());

		public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
			CancellationToken cancellationToken,
			DeadLetterQueryFilter? filter = null,
			int limit = 100) => Task.FromResult<IReadOnlyList<DeadLetterEntry>>([]);

		public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult<DeadLetterEntry?>(null);

		public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken)
		{
			if (!_existing.Contains(entryId))
			{
				return Task.FromResult(false);
			}

			ReplayedIds.Add(entryId); // records the re-dispatch effect
			return Task.FromResult(true);
		}

		public Task<long> GetCountAsync(CancellationToken cancellationToken, DeadLetterQueryFilter? filter = null) =>
			Task.FromResult((long)_existing.Count);

		public Task<ReplayBatchResult> ReplayBatchAsync(
			DeadLetterQueryFilter filter,
			int limit,
			CancellationToken cancellationToken)
		{
			BatchReplayCount++;

			// Honour the caller's limit, so this fake cannot certify a behaviour the real store does not
			// have: a fake that silently replays past the limit would make a truncation regression invisible.
			var selected = _existing.Take(limit).ToList();
			foreach (var id in selected)
			{
				ReplayedIds.Add(id);
			}

			return Task.FromResult(
				new ReplayBatchResult(selected.Count, selected.Count, Truncated: _existing.Count > limit));
		}

		public Task<bool> PurgeAsync(Guid entryId, CancellationToken cancellationToken) => Task.FromResult(true);

		public Task<int> PurgeAllTenantsEntriesOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken) =>
			Task.FromResult(0);
	}

	/// <summary>Authenticates every request as a fixed operator principal → passes the default policy.</summary>
	private sealed class AcceptingAuthHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
	{
		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "dashboard-operator")], TestScheme);
			var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}
