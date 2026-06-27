// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Integration.Tests.Data.Saga;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

namespace Excalibur.Integration.Tests.Data.ChangeFeed;

/// <summary>
/// NON-SKIPPED real-Cosmos regression lock for bead <c>egwtku</c> (sprint 855, FR-B/durable continuation):
/// the durable <see cref="CosmosDbChangeFeedCheckpointStore"/> MUST persist a change-feed continuation
/// token so a subscription resumes <b>after a process restart</b> instead of replaying from the beginning
/// of the feed.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>). Platform's egwtku impl adds the
/// <see cref="IChangeFeedCheckpointStore"/> seam (default <c>InMemoryChangeFeedCheckpointStore</c>, durable
/// <see cref="CosmosDbChangeFeedCheckpointStore"/>) wired load-on-start / persist-after-batch into
/// <c>CosmosDbChangeFeedSubscription&lt;T&gt;</c>. The internal store is reached via
/// <c>InternalsVisibleTo(Excalibur.Integration.Tests)</c> (PM 17088).
/// </para>
/// <para>
/// <b>Real-infra, NON-SKIPPED</b> (NFR-1): runs against the real Cosmos emulator via
/// <see cref="CosmosDbSagaStoreContainerFixture"/>. <c>IsInitialized</c> is asserted (hard requirement) so
/// the suite cannot silently skip the durability proof. Asserts the <i>observed</i> persistence across a
/// fresh store instance (simulating a process restart), not that an option was set.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the non-durable default):</b> a fresh <c>InMemoryChangeFeedCheckpointStore</c>
/// returns <see langword="null"/> after "restart" (process-local) → reprocess-from-start. The durable
/// Cosmos store returns the persisted token, so the assertion below distinguishes durable from in-memory.
/// </para>
/// <para>
/// <b>DEFAULT-serializer client (bd-i2eabb):</b> the checkpoint container is created from a Cosmos client
/// built WITHOUT <c>WithSystemTextJsonSerializerOptions</c> — i.e. the SDK-v3 <b>default Newtonsoft</b>
/// serializer, which is the most common production config. The shared fixture's <c>Client</c> is
/// STJ-configured, and an STJ client would serialize <c>CheckpointDocument</c> lowercase regardless of the
/// document's attributes — so it would pass while the default-serializer prod path is inert. Exercising the
/// default serializer here makes the round-trip RED on the STJ-only attribute defect and GREEN on the
/// dual-attribute fix (verify-against-real-infra-not-mock).
/// </para>
/// <para>
/// <b>CARRY-WITH-FLAG (egwtku, operator bead <c>jattxa</c>):</b> the bundled Cosmos emulator image is
/// expired (license eval lapsed → container <c>Exited(1)</c>, "evaluation period has expired"), so this
/// lock is authored + compile-verified + committed but has <b>not</b> been observed GREEN. It RED→GREENs
/// the instant a valid Cosmos emulator is provisioned. Per the ruling (SA 17025 / SEAM-4 / EC-2): NOT
/// skip-to-green, NOT mock-pass, does NOT false-block S855 close.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "ChangeFeed")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbChangeFeedCheckpointStoreDurabilityShould
	: IClassFixture<CosmosDbSagaStoreContainerFixture>, IDisposable
{
	private readonly CosmosDbSagaStoreContainerFixture _fixture;
	private CosmosClient? _defaultSerializerClient;

	public CosmosDbChangeFeedCheckpointStoreDurabilityShould(CosmosDbSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	public void Dispose() => _defaultSerializerClient?.Dispose();

	[Fact]
	public async Task PersistContinuationTokenAcrossAFreshStoreInstance()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"Cosmos emulator must be available — real-Cosmos durable-continuation proof (NFR-1)");

		var container = await CreateCheckpointsContainerAsync().ConfigureAwait(false);
		var subscriptionId = $"sub-egwtku-{Guid.NewGuid():N}";
		const string token = "continuation-token-egwtku-42";

		// Persist via one store instance...
		IChangeFeedCheckpointStore writer = new CosmosDbChangeFeedCheckpointStore(container);
		await writer.SaveAsync(subscriptionId, token, CancellationToken.None).ConfigureAwait(false);

		// ...then load via a FRESH instance — simulates a process restart. Durable continuation means the
		// persisted token survives; the InMemory default would return null here (reprocess-from-start).
		IChangeFeedCheckpointStore reader = new CosmosDbChangeFeedCheckpointStore(container);
		var loaded = await reader.LoadAsync(subscriptionId, CancellationToken.None).ConfigureAwait(false);

		loaded.ShouldBe(token);
	}

	[Fact]
	public async Task ReturnNullWhenNoCheckpointHasBeenPersisted()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"Cosmos emulator must be available — real-Cosmos durable-continuation proof (NFR-1)");

		var container = await CreateCheckpointsContainerAsync().ConfigureAwait(false);
		IChangeFeedCheckpointStore store = new CosmosDbChangeFeedCheckpointStore(container);

		// No checkpoint persisted → null → the subscription starts from the configured position (NotFound
		// is swallowed, not surfaced as an error).
		var loaded = await store.LoadAsync($"absent-{Guid.NewGuid():N}", CancellationToken.None).ConfigureAwait(false);

		loaded.ShouldBeNull();
	}

	private async Task<Container> CreateCheckpointsContainerAsync()
	{
		// Use a DEFAULT-serializer (Newtonsoft) client — NOT the fixture's STJ-configured Client — so the
		// checkpoint document round-trips through the same serializer a typical production CosmosClient uses.
		// An STJ client would emit lowercase keys regardless of the document's attributes and mask the defect
		// (bd-i2eabb / verify-against-real-infra-not-mock).
		var client = GetOrCreateDefaultSerializerClient();
		var database = client.GetDatabase(_fixture.DatabaseName);
		var response = await database.CreateContainerIfNotExistsAsync(
			new ContainerProperties($"changefeed-checkpoints-{Guid.NewGuid():N}", "/subscriptionId"))
			.ConfigureAwait(false);
		return response.Container;
	}

	private CosmosClient GetOrCreateDefaultSerializerClient()
	{
		// Built from the same emulator connection + self-signed-cert HttpClient as the fixture, but deliberately
		// WITHOUT WithSystemTextJsonSerializerOptions → the SDK-v3 default Newtonsoft serializer (the prod default).
		return _defaultSerializerClient ??= new CosmosClientBuilder(_fixture.ConnectionString)
			.WithConnectionModeGateway()
			.WithHttpClientFactory(() => _fixture.EmulatorHttpClient)
			.Build();
	}
}
