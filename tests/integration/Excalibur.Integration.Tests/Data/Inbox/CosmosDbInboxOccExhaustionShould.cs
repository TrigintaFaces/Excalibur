// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Data.CosmosDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// The Cosmos DB inbox store's handling of optimistic-concurrency EXHAUSTION on mark-failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves, and what it does not.</b> It proves the STORE's loop: given N consecutive
/// precondition failures at a bound of N, the store reports <see cref="InboxMarkFailedOutcome.Undecided"/>,
/// does not throw, and attempted exactly N conditional replaces. It does NOT prove that concurrent writers
/// produce those failures -- the staleness here is forced by the handler below, not by a rival writer.
/// That is the real-infrastructure lost-race arms' job, and a green here must not be read as discharging it.
/// </para>
/// <para>
/// The failures themselves are genuine: every request reaches the real emulator, and the handler only
/// swaps the replace's <c>If-Match</c> for an ETag the server has never issued. The server then refuses it
/// with a real <c>412</c>, which the real SDK parses into its real exception. Nothing here fakes an SDK type.
/// </para>
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "CosmosDb")]
[Trait("Infrastructure", "CosmosEmulator")]
public sealed class CosmosDbInboxOccExhaustionShould
{
	private const int Bound = 3;

	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private readonly string _containerName = $"inbox_occ_{Guid.NewGuid():N}";

	public CosmosDbInboxOccExhaustionShould(CosmosDbEventStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ReportUndecided_AfterExactlyTheBoundOfPreconditionFailures_AndNotThrow()
	{
		var handler = new StaleEtagHandler { ForceStale = false, InnerHandler = _fixture.EmulatorHttpMessageHandler };
		var store = await CreateStoreAsync(handler).ConfigureAwait(false);
		await SeedAsync(store).ConfigureAwait(false);

		handler.ResetCount();
		handler.ForceStale = true;

		var outcome = await store.MarkFailedAsync("m-1", "h-1", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(
			InboxMarkFailedOutcome.Undecided,
			"contention that outlasts the bound is reported as Undecided so the caller can re-drive it");
		handler.ConditionalReplaces.ShouldBe(
			Bound,
			"the store must attempt the conditional replace exactly once per permitted attempt -- fewer means "
			+ "it gave up early, more means the bound is not the bound");
	}

	/// <summary>
	/// CONTROL for the fixture: the same path with the ETag left alone yields Applied after ONE replace.
	/// Without it, the arm above could be green because the handler broke every request, not just the
	/// precondition.
	/// </summary>
	[Fact]
	public async Task ReportApplied_AfterOneReplace_WhenTheEtagIsCurrent()
	{
		var handler = new StaleEtagHandler { ForceStale = false, InnerHandler = _fixture.EmulatorHttpMessageHandler };
		var store = await CreateStoreAsync(handler).ConfigureAwait(false);
		await SeedAsync(store).ConfigureAwait(false);

		handler.ResetCount();

		var outcome = await store.MarkFailedAsync("m-1", "h-1", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(InboxMarkFailedOutcome.Applied);
		handler.ConditionalReplaces.ShouldBe(1, "an accepted replace must not be retried");
	}

	private static Task SeedAsync(CosmosDbInboxStore store) =>
		store.CreateEntryAsync(
			"m-1",
			"h-1",
			"TestMessage",
			Encoding.UTF8.GetBytes("{}"),
			new Dictionary<string, object>(),
			CancellationToken.None).AsTask();

	private async Task<CosmosDbInboxStore> CreateStoreAsync(StaleEtagHandler handler)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB emulator must be available - real-infra arms are never skipped. " + _fixture.InitializationError);

		var store = new CosmosDbInboxStore(
			Options.Create(new CosmosDbInboxOptions
			{
				DatabaseName = _fixture.DatabaseName,
				ContainerName = _containerName,
				CreateContainerIfNotExists = true,
				DefaultTimeToLiveSeconds = 0,
				MaxConcurrencyRetries = Bound,
				Client = new CosmosDbClientOptions
				{
					ConnectionString = _fixture.ConnectionString,
					UseDirectMode = false,
					HttpClientFactory = () => new HttpClient(handler, disposeHandler: false),
				},
			}),
			NullLogger<CosmosDbInboxStore>.Instance,
			SingleTenantTestContext.Instance);

		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Passes every request through to the real emulator; counts conditional replaces and, when told to,
	/// makes each one present an ETag the server never issued.
	/// </summary>
	private sealed class StaleEtagHandler : DelegatingHandler
	{
		private const string NeverIssuedEtag = "\"00000000-0000-0000-0000-000000000000\"";
		private bool _forceStale;
		private int _conditionalReplaces;

		public bool ForceStale
		{
			get => Volatile.Read(ref _forceStale);
			set => Volatile.Write(ref _forceStale, value);
		}

		public int ConditionalReplaces => Volatile.Read(ref _conditionalReplaces);

		public void ResetCount() => Volatile.Write(ref _conditionalReplaces, 0);

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var isConditionalReplace = request.Method == HttpMethod.Put
				&& request.RequestUri?.AbsolutePath.Contains("/docs/", StringComparison.Ordinal) == true
				&& request.Headers.Contains("If-Match");

			if (isConditionalReplace)
			{
				_ = Interlocked.Increment(ref _conditionalReplaces);

				if (ForceStale)
				{
					_ = request.Headers.Remove("If-Match");
					_ = request.Headers.TryAddWithoutValidation("If-Match", NeverIssuedEtag);
				}
			}

			return base.SendAsync(request, cancellationToken);
		}
	}
}
