// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.CosmosDb;
using Excalibur.Outbox;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-emulator locks on client ownership: a host that enables several Cosmos features opens one
/// connection pool against the account, not one per feature.
/// </summary>
/// <remarks>
/// <para>
/// Every Cosmos package registers a shared <see cref="CosmosClient"/> singleton. If the store cannot reach
/// it — because its only constructor takes no client, or because the registration hand-constructs the store
/// and names a constructor that does not — the store builds a private client instead, and the host ends up
/// with one client per enabled feature plus the registered one nobody uses. Each client carries its own
/// connection pool against the same account.
/// </para>
/// <para>
/// This is asserted by resolving the services and comparing instances, not by reading the registrations. A
/// registration-shape assertion cannot see the defect: the singleton descriptor is present either way, and
/// it is present precisely in the broken case.
/// </para>
/// <para>
/// The ownership half matters as much as the sharing half. A store that borrows a client and still disposes
/// it would tear the pool out from under every other feature still running on it, so the safety arm disposes
/// one feature's store and then requires the other to keep working.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbSharedClientAcrossFeaturesShould
	: IClassFixture<CosmosDbOutboxStoreContainerFixture>
{
	private readonly CosmosDbOutboxStoreContainerFixture _fixture;

	public CosmosDbSharedClientAcrossFeaturesShould(CosmosDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ShareTheRegisteredClient_WhenAHostEnablesBothOutboxAndInbox()
	{
		_fixture.DockerAvailable.ShouldBeTrue("The Cosmos DB emulator must be available -- never skipped.");

		var ct = TestContext.Current.CancellationToken;
		var outboxContainer = $"outbox_{Guid.NewGuid():N}";
		var inboxContainer = $"inbox_{Guid.NewGuid():N}";

		// The client a host registers. Built with the emulator's HttpClient because the emulator presents a
		// self-signed certificate; a default client cannot complete a connection to it.
		using var registeredClient = new CosmosClient(
			_fixture.ConnectionString,
			new CosmosClientOptions
			{
				ConnectionMode = ConnectionMode.Gateway,
				HttpClientFactory = () => _fixture.EmulatorHttpClient,
				// The same serializer the framework's own client registration and the stores' client factory
				// both configure. It has to match: the store now uses whatever client it is handed, so a
				// client serializing differently would change the stored document shape rather than merely
				// the connection pool.
				UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
				{
					PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
				}
			});

		var services = new ServiceCollection();
		_ = services.AddLogging();

		var outboxBuilder = A.Fake<IOutboxBuilder>();
		A.CallTo(() => outboxBuilder.Services).Returns(services);
		_ = outboxBuilder.UseCosmosDb(cosmos => cosmos
			.Client(registeredClient)
			.DatabaseName(_fixture.DatabaseName)
			.ContainerName(outboxContainer));

		var inboxBuilder = A.Fake<IInboxBuilder>();
		A.CallTo(() => inboxBuilder.Services).Returns(services);
		_ = inboxBuilder.UseCosmosDb(cosmos => cosmos
			.Client(registeredClient)
			.DatabaseName(_fixture.DatabaseName)
			.ContainerName(inboxContainer));

		await using var provider = services.BuildServiceProvider();

		var singleton = provider.GetRequiredService<CosmosClient>();
		singleton.ShouldBeSameAs(
			registeredClient,
			"the client the host supplied through the builder must be the one registered.");

		var outboxStore = provider.GetRequiredService<CosmosDbOutboxStore>();
		var inboxStore = provider.GetRequiredService<CosmosDbInboxStore>();

		await outboxStore.InitializeAsync(ct).ConfigureAwait(false);
		await inboxStore.InitializeAsync(ct).ConfigureAwait(false);

		// THE MEASUREMENT. Both stores hold the registered instance itself, not a private copy built from
		// the same connection string. Reference identity is the whole point: two clients configured
		// identically are still two connection pools.
		ClientHeldBy(outboxStore).ShouldBeSameAs(
			singleton,
			"the outbox store built its own client while a configured one was already registered, so this "
			+ "host holds two connection pools against the same account and every setting the consumer "
			+ "supplied on the registered client -- handler, resilience, timeouts, failover -- is inert on "
			+ "the outbox path.");

		ClientHeldBy(inboxStore).ShouldBeSameAs(
			singleton,
			"the inbox store built its own client while a configured one was already registered, so this "
			+ "host holds a third connection pool against the same account.");

		// LIVENESS -- borrowing is worthless if the stores no longer function. Both must do real work
		// through the shared client.
		var partitionKey = new Excalibur.Data.CloudNative.PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = new CloudOutboxMessage
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKey.Value
		};

		var added = await outboxStore.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
		added.Success.ShouldBeTrue($"the outbox store must work through the shared client: {added.ErrorMessage}");

		var handlerType = $"handler-{Guid.NewGuid():N}";
		var inboxMessageId = $"inbox-{Guid.NewGuid():N}";

		(await inboxStore.IsProcessedAsync(inboxMessageId, handlerType, ct).ConfigureAwait(false))
			.ShouldBeFalse("the inbox store must work through the shared client.");

		// SAFETY -- one feature shutting down must not take the shared pool with it. A store that disposed
		// a client it borrowed would break every other feature still running on the same account.
		await outboxStore.DisposeAsync().ConfigureAwait(false);

		(await inboxStore.IsProcessedAsync(inboxMessageId, handlerType, ct).ConfigureAwait(false))
			.ShouldBeFalse(
				"disposing the outbox store disposed the client it had borrowed, so the inbox store -- which "
				+ "is still running on that same client -- can no longer reach the account.");

		await inboxStore.DisposeAsync().ConfigureAwait(false);
		await _fixture.CleanupContainerAsync(outboxContainer).ConfigureAwait(false);
		await _fixture.CleanupContainerAsync(inboxContainer).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the client a store is actually using. The field is private because a consumer has no business
	/// with it; the identity of that instance is nevertheless the property under test, and nothing on the
	/// public surface exposes it.
	/// </summary>
	private static CosmosClient? ClientHeldBy(object store)
	{
		var field = store.GetType().GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull($"{store.GetType().Name} must hold its client in a '_client' field.");
		return (CosmosClient?)field.GetValue(store);
	}
}
