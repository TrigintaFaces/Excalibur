// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Locks the seam that carries the host's source-generated JSON type-info resolver through the DynamoDB
/// provider's own registration extension and into the store's serialization.
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
/// Against a real DynamoDB (LocalStack) and through <c>UseDynamoDb</c>, not by constructing the store by
/// hand: a store that accepts a resolver nobody hands it is still broken for every consumer, so the
/// registration is the half that matters. The DynamoDB builder exposes no resolver method of its own, so
/// the consumer surface is a <c>Configure</c> on the shipped options type - which is what this suite
/// exercises, alongside the builder rather than instead of it. LocalStack is required, never skip-gated: an
/// arm that passes by not running is the gap that ships the bug.
/// </para>
/// <para>
/// The rejection arm and the reflection arm together are the discriminator. Each hands the SAME undeclared
/// event to the SAME registration; the only variable is whether a resolver was configured. A store that
/// ignored the resolver would take the reflection path in both, so both would succeed and the rejection arm
/// would fail. That is what makes this a statement about the resolver rather than about the event type.
/// </para>
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbEventStoreEventTypeInfoResolverShould
	: IClassFixture<DynamoDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "DdbResolverAggregate";

	private readonly DynamoDbEventStoreContainerFixture _fixture;
	private readonly string _tableName = $"events_{Guid.NewGuid():N}";

	public DynamoDbEventStoreEventTypeInfoResolverShould(DynamoDbEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The resolver seam is verified against a real DynamoDB, through the provider's own registration. "
			+ $"This suite must never be skipped: {_fixture.InitializationError}");

		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync() =>
		await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);


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
			[new UndeclaredDdbTestEvent { AggregateId = aggregateId }],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue(result.ErrorMessage);
	}

	[Fact]
	public async Task Write_ByteIdenticalPayloads_WithAndWithoutAResolver()
	{
		var withoutResolver = await AppendAndLoadAsync(resolver: null).ConfigureAwait(false);
		var withResolver = await AppendAndLoadAsync(DdbResolverTestEventContext.Default).ConfigureAwait(false);

		// The aggregate id differs per append, and the event body carries it; the metadata blob does not.
		withResolver.Metadata.ShouldNotBeNull();
		withResolver.Metadata.ShouldBe(withoutResolver.Metadata);
		withResolver.EventType.ShouldBe(withoutResolver.EventType);
		withResolver.EventData.Length.ShouldBe(withoutResolver.EventData.Length);
	}

	[Fact]
	public async Task Write_PayloadsTheReflectionPathReadsBack()
	{
		var stored = await AppendAndLoadAsync(DdbResolverTestEventContext.Default).ConfigureAwait(false);

		// Read back through the canonical reflection options a differently-configured host would use.
		var roundTripped = JsonSerializer.Deserialize<DdbResolverTestEvent>(
			stored.EventData, EventSerializationDefaults.Canonical);

		roundTripped.ShouldNotBeNull();
		roundTripped.EventId.ShouldBe("e-1");
		roundTripped.Name.ShouldBe("order-placed");
		roundTripped.Shade.ShouldBe(DdbResolverTestShade.Green);

		var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
			stored.Metadata!, EventSerializationDefaults.Canonical);

		metadata.ShouldNotBeNull();
		metadata["UserId"].GetString().ShouldBe("u-1");
		metadata["Attempt"].GetInt32().ShouldBe(2);
		metadata["Replayed"].GetBoolean().ShouldBeTrue();
		metadata["Absent"].ValueKind.ShouldBe(JsonValueKind.Null);
	}

	private ServiceProvider BuildProvider(IJsonTypeInfoResolver? resolver)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseDynamoDb(dynamo =>
			_ = dynamo
				.Client(_fixture.Client)
				.TableName(_tableName))));

		if (resolver is not null)
		{
			// The consumer surface: the DynamoDB builder carries no resolver method, so the resolver is set
			// on the shipped options type. Registered AFTER the builder deliberately - the builder's own
			// Configure never touches EventTypeInfoResolver, so this is order-independent, and running it
			// second is the shape a consumer's own Configure call would take.
			_ = services.Configure<DynamoDbEventStoreOptions>(options => options.EventTypeInfoResolver = resolver);
		}

		return services.BuildServiceProvider();
	}

	private async Task<StoredEvent> AppendAndLoadAsync(IJsonTypeInfoResolver? resolver)
	{
		await using var provider = BuildProvider(resolver);
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var aggregateId = Guid.NewGuid().ToString();

		var appended = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		appended.Success.ShouldBeTrue(appended.ErrorMessage);

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		return loaded.ShouldHaveSingleItem();
	}

	private static DdbResolverTestEvent CreateEvent(string aggregateId) => new()
	{
		EventId = "e-1",
		AggregateId = aggregateId,
		OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
		Name = "order-placed",
		Shade = DdbResolverTestShade.Green,
		Metadata = new Dictionary<string, object>
		{
			["UserId"] = "u-1",
			["Attempt"] = 2,
			["Replayed"] = true,
			["Absent"] = null!,
		},
	};
}

internal enum DdbResolverTestShade
{
	Red,
	Green,
}

internal sealed class DdbResolverTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(DdbResolverTestEvent);

	public string Name { get; set; } = string.Empty;

	public DdbResolverTestShade Shade { get; set; }

	public IDictionary<string, object>? Metadata { get; set; }
}

internal sealed class UndeclaredDdbTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(UndeclaredDdbTestEvent);

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
[JsonSerializable(typeof(DdbResolverTestEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class DdbResolverTestEventContext : JsonSerializerContext;
