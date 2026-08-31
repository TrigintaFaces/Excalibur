// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Excalibur.Data.CloudNative;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the DynamoDB event store to the registration a consumer actually writes: every arm builds a real
/// <see cref="ServiceProvider"/> from <c>UseDynamoDb</c> and resolves the store out of it.
/// </summary>
/// <remarks>
/// <para>
/// A store constructed by hand cannot show whether the container can build one, which is why the defect
/// this locks reached CI behind a green suite: the store's only constructor required an
/// <see cref="IAmazonDynamoDBStreams"/>, <c>UseDynamoDb</c> registered no such client on any path, and
/// every test that exercised the store handed it both clients directly. Resolution is therefore the
/// assertion here, not the setup.
/// </para>
/// <para>
/// The Streams client backs the change feed alone. A host that event-sources on DynamoDB without consuming
/// a change feed must still be able to build the store, so it is resolved optionally; the arms below cover
/// each supported registration shape, with and without one. Each is paired with a liveness arm - a store
/// that resolves but cannot round-trip an append satisfies a resolution assertion on its own.
/// </para>
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "DynamoDb")]
[Trait("Component", "EventStore")]
public sealed class DynamoDbEventStoreRegistrationShould : IClassFixture<DynamoDbEventStoreContainerFixture>
{
	private const string AggregateType = "RegistrationAggregate";
	private readonly DynamoDbEventStoreContainerFixture _fixture;

	public DynamoDbEventStoreRegistrationShould(DynamoDbEventStoreContainerFixture fixture) => _fixture = fixture;

	private ServiceProvider BuildProvider(Action<IDynamoDBEventSourcingBuilder> configure)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra registration is never skipped: "
			+ $"{_fixture.InitializationError}");

		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseDynamoDb(configure)));
		return services.BuildServiceProvider();
	}

	// Each arm supplies its OWN clients rather than the fixture's shared pair: a container disposes the
	// singletons it holds, so registering the shared clients would dispose them out from under the arms
	// that run next.
	private static AmazonDynamoDBClient NewClient(string serviceUrl) =>
		new(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig { ServiceURL = serviceUrl });

	private static AmazonDynamoDBStreamsClient NewStreamsClient(string serviceUrl) =>
		new(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBStreamsConfig { ServiceURL = serviceUrl });

	private Action<IDynamoDBEventSourcingBuilder> SuppliedClient(bool withStreams)
	{
		var serviceUrl = _fixture.ServiceUrl;

		return dynamo =>
		{
			_ = dynamo.Client(NewClient(serviceUrl)).TableName($"events_{Guid.NewGuid():N}");

			if (withStreams)
			{
				_ = dynamo.StreamsClient(NewStreamsClient(serviceUrl));
			}
		};
	}

	private static async Task ShouldRoundTripAsync(IEventStore store)
	{
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		var events = new[]
		{
			new TestDomainEvent { AggregateId = aggregateId, OccurredAt = DateTimeOffset.UtcNow, Data = "first" },
			new TestDomainEvent { AggregateId = aggregateId, OccurredAt = DateTimeOffset.UtcNow, Data = "second" },
		};

		var result = await store.AppendAsync(aggregateId, AggregateType, events, expectedVersion: -1, CancellationToken.None);
		result.Success.ShouldBeTrue("a resolved store must be able to append - resolution alone proves nothing");

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(2, "the appended events must load back through the resolved store");
	}

	/// <summary>
	/// LIVENESS: a consumer supplying only a DynamoDB client resolves a working store. This is the shape
	/// that could not be constructed at all, and the one a host without a change feed uses.
	/// </summary>
	[Fact]
	public async Task Resolve_and_round_trip_when_only_a_dynamo_client_is_supplied()
	{
		await using var provider = BuildProvider(SuppliedClient(withStreams: false));

		var store = provider.GetRequiredService<IEventStore>();
		_ = store.ShouldBeOfType<DynamoDbEventStore>();

		await ShouldRoundTripAsync(store);
	}

	/// <summary>LIVENESS: supplying both clients resolves a working store and a usable change feed.</summary>
	[Fact]
	public async Task Resolve_a_change_feed_when_a_streams_client_is_supplied()
	{
		await using var provider = BuildProvider(SuppliedClient(withStreams: true));

		var store = provider.GetRequiredService<IEventStore>();
		await ShouldRoundTripAsync(store);

		var cloudStore = store.ShouldBeAssignableTo<ICloudNativeEventStore>()!;
		var changeFeed = cloudStore.GetService(typeof(ICloudNativeEventStoreChangeFeed))
			.ShouldBeAssignableTo<ICloudNativeEventStoreChangeFeed>()!;

		await using var subscription = await changeFeed.SubscribeToChangesAsync(null, CancellationToken.None);
		subscription.ShouldNotBeNull("a supplied Streams client must produce a change-feed subscription");
	}

	/// <summary>
	/// LIVENESS: configuring by service URL leaves the registration owning the connection, so it builds the
	/// Streams client itself and the change feed works with no further configuration.
	/// </summary>
	[Fact]
	public async Task Build_a_streams_client_itself_when_it_owns_the_connection()
	{
		await using var provider = BuildProvider(dynamo =>
			_ = dynamo.ServiceUrl(_fixture.ServiceUrl).TableName($"events_{Guid.NewGuid():N}"));

		_ = provider.GetService<IAmazonDynamoDBStreams>().ShouldNotBeNull(
			"a registration that owns the connection must register a matching Streams client");

		var store = provider.GetRequiredService<IEventStore>();
		_ = store.ShouldBeOfType<DynamoDbEventStore>();
	}

	/// <summary>LIVENESS: the client-factory shape resolves and round-trips like the instance shape.</summary>
	[Fact]
	public async Task Resolve_and_round_trip_through_the_client_factory_shape()
	{
		var serviceUrl = _fixture.ServiceUrl;
		await using var provider = BuildProvider(dynamo =>
			_ = dynamo.ClientFactory(_ => NewClient(serviceUrl)).TableName($"events_{Guid.NewGuid():N}"));

		await ShouldRoundTripAsync(provider.GetRequiredService<IEventStore>());
	}

	/// <summary>
	/// SAFETY: without a Streams client the change feed refuses with an explanation naming the fix, rather
	/// than serving an empty feed. The store still resolves - that is the arm above.
	/// </summary>
	[Fact]
	public async Task Refuse_a_change_feed_when_no_streams_client_was_supplied()
	{
		await using var provider = BuildProvider(SuppliedClient(withStreams: false));

		var store = provider.GetRequiredService<IEventStore>();

		// A consumer probing for the capability is told it is absent, rather than handed an instance that
		// throws on first use.
		store.ShouldBeAssignableTo<ICloudNativeEventStore>()!
			.GetService(typeof(ICloudNativeEventStoreChangeFeed))
			.ShouldBeNull("the change feed cannot be served without a Streams client, so it must not be advertised");

		// Reached directly rather than through the probe, it refuses with an explanation naming the fix.
		var changeFeed = store.ShouldBeAssignableTo<ICloudNativeEventStoreChangeFeed>()!;

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await changeFeed.SubscribeToChangesAsync(null, CancellationToken.None));

		thrown.Message.ShouldContain("StreamsClient",
			Case.Sensitive,
			"the refusal must name the registration call that supplies one");
	}
}
