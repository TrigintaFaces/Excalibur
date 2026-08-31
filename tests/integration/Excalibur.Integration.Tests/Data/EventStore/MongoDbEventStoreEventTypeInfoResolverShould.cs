// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Locks the seam that carries <see cref="MongoDbEventStoreOptions.EventTypeInfoResolver"/> through the
/// MongoDB provider's own registration extension and into the store's serialization.
/// </summary>
/// <remarks>
/// <para>
/// The defect this guards is invisible to a build. Publishing the store's serialization path with
/// reflection-based serialization disabled raises no IL warning either way, and fails only when the process
/// appends its first event. So the assertions here are behavioural: an event type the host's resolver does
/// not declare must fail, which it can only do if the store consults that resolver at all. Against a store
/// that builds its own options and ignores the host's, every one of those appends succeeds through
/// reflection.
/// </para>
/// <para>
/// Against a real MongoDB and through <c>UseMongoDB</c>, not by constructing the store by hand: a store
/// that accepts a resolver nobody hands it is still broken for every consumer, so the registration is the
/// half that matters. The container is required, never skip-gated.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbEventStoreEventTypeInfoResolverShould
	: IClassFixture<MongoDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "MongoResolverAggregate";

	private readonly MongoDbEventStoreContainerFixture _fixture;

	public MongoDbEventStoreEventTypeInfoResolverShould(MongoDbEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The resolver seam is verified against a real MongoDB, through the provider's own registration. "
			+ "This suite must never be skipped.");

		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;


	[Fact]
	public async Task Serialize_ThroughReflection_WhenNoResolverIsConfigured()
	{
		// The default path must be unchanged: with no resolver the store serializes anything, including the
		// type the resolver-configured registration above refuses. This is what makes the rejection arm a
		// statement about the resolver rather than about the event type.
		await using var provider = BuildProvider(resolver: null);
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var aggregateId = Guid.NewGuid().ToString();

		var result = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[new UndeclaredMongoTestEvent { AggregateId = aggregateId }],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Write_ByteIdenticalPayloads_WithAndWithoutAResolver()
	{
		var withoutResolver = await AppendAndLoadAsync(resolver: null).ConfigureAwait(false);
		var withResolver = await AppendAndLoadAsync(MongoResolverTestEventContext.Default).ConfigureAwait(false);

		// The aggregate id differs per append, and the event body carries it; the metadata blob does not.
		withResolver.Metadata.ShouldNotBeNull();
		withResolver.Metadata.ShouldBe(withoutResolver.Metadata);
		withResolver.EventType.ShouldBe(withoutResolver.EventType);
		withResolver.EventData.Length.ShouldBe(withoutResolver.EventData.Length);
	}

	[Fact]
	public async Task Write_PayloadsTheReflectionPathReadsBack()
	{
		var stored = await AppendAndLoadAsync(MongoResolverTestEventContext.Default).ConfigureAwait(false);

		// Read back through the canonical reflection options a differently-configured host would use.
		var roundTripped = JsonSerializer.Deserialize<MongoResolverTestEvent>(
			stored.EventData, EventSerializationDefaults.Canonical);

		roundTripped.ShouldNotBeNull();
		roundTripped.EventId.ShouldBe("e-1");
		roundTripped.Name.ShouldBe("order-placed");
		roundTripped.Shade.ShouldBe(MongoResolverTestShade.Green);

		var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
			stored.Metadata!, EventSerializationDefaults.Canonical);

		metadata.ShouldNotBeNull();
		metadata["UserId"].GetString().ShouldBe("u-1");
		metadata["Attempt"].GetInt32().ShouldBe(2);
		metadata["Replayed"].GetBoolean().ShouldBeTrue();
		metadata["Absent"].ValueKind.ShouldBe(JsonValueKind.Null);
	}

	private ServiceProvider BuildProvider(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? resolver)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseMongoDB(mongo =>
			_ = mongo
				.ConnectionString(_fixture.ConnectionString)
				.DatabaseName(_fixture.DatabaseName))));

		if (resolver is not null)
		{
			// The provider's own Configure delegate sets only its connection/collection properties, so a
			// consumer's Configure composes with it rather than overwriting it.
			_ = services.Configure<MongoDbEventStoreOptions>(options => options.EventTypeInfoResolver = resolver);
		}

		return services.BuildServiceProvider();
	}

	private async Task<StoredEvent> AppendAndLoadAsync(
		System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? resolver)
	{
		await using var provider = BuildProvider(resolver);
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var aggregateId = Guid.NewGuid().ToString();

		_ = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		return loaded.ShouldHaveSingleItem();
	}

	private static MongoResolverTestEvent CreateEvent(string aggregateId) => new()
	{
		EventId = "e-1",
		AggregateId = aggregateId,
		OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
		Name = "order-placed",
		Shade = MongoResolverTestShade.Green,
		Metadata = new Dictionary<string, object>
		{
			["UserId"] = "u-1",
			["Attempt"] = 2,
			["Replayed"] = true,
			["Absent"] = null!,
		},
	};
}

internal enum MongoResolverTestShade
{
	Red,
	Green,
}

internal sealed class MongoResolverTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(MongoResolverTestEvent);

	public string Name { get; set; } = string.Empty;

	public MongoResolverTestShade Shade { get; set; }

	public IDictionary<string, object>? Metadata { get; set; }
}

internal sealed class UndeclaredMongoTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(UndeclaredMongoTestEvent);

	public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// A consumer-shaped source-generated context, deliberately carrying no
/// <c>JsonSourceGenerationOptions</c> annotation.
/// </summary>
/// <remarks>
/// The store attaches the resolver to its own canonical options rather than adopting the context's, so the
/// naming policy, string-enum representation and null handling that fix the stored wire format do not depend
/// on how the host annotated its context. A bare context is therefore the stricter fixture: if the byte
/// comparison holds for this one, a consumer cannot mis-annotate their way to a divergent payload.
/// <c>Dictionary&lt;string, object&gt;</c> is not declared here; only the closed metadata value types are.
/// </remarks>
[JsonSerializable(typeof(MongoResolverTestEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class MongoResolverTestEventContext : JsonSerializerContext;
