// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Locks the seam that carries <see cref="InMemoryEventStoreOptions.EventTypeInfoResolver"/> through to the
/// store's serialization.
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
/// The wire format is asserted by comparing the bytes the two paths write, not by reading them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class InMemoryEventStoreEventTypeInfoResolverShould : UnitTestBase
{
	private const string AggregateType = "ResolverAggregate";

	[Fact]
	public async Task Reject_AnEventTypeTheHostResolverDoesNotDeclare()
	{
		var store = CreateStore(ResolverTestEventContext.Default);
		var aggregateId = Guid.NewGuid().ToString();

		// UndeclaredTestEvent is deliberately absent from the context. A store that consults the host's
		// resolver has no metadata for it and cannot serialize it; a store that quietly built its own
		// reflection options serializes it happily, which is the defect.
		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				aggregateId,
				AggregateType,
				[new UndeclaredTestEvent { AggregateId = aggregateId }],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		thrown.Message.ShouldContain(nameof(UndeclaredTestEvent));
	}

	[Fact]
	public async Task Reject_AMetadataValueTypeTheHostResolverDoesNotDeclare()
	{
		var store = CreateStore(ResolverTestEventContext.Default);
		var aggregateId = Guid.NewGuid().ToString();

		// Metadata values are written as their runtime type, so each runtime type must itself be declared.
		// Guid is not on the context; string, int and bool are.
		var domainEvent = new ResolverTestEvent
		{
			AggregateId = aggregateId,
			Metadata = new Dictionary<string, object> { ["TraceId"] = Guid.NewGuid() },
		};

		_ = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				aggregateId,
				AggregateType,
				[domainEvent],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Write_ByteIdenticalPayloads_WithAndWithoutAResolver()
	{
		var aggregateId = Guid.NewGuid().ToString();

		var withoutResolver = await AppendAndLoadAsync(CreateStore(resolver: null), aggregateId)
			.ConfigureAwait(false);
		var withResolver = await AppendAndLoadAsync(CreateStore(ResolverTestEventContext.Default), aggregateId)
			.ConfigureAwait(false);

		withResolver.EventData.ShouldBe(withoutResolver.EventData);
		withResolver.Metadata.ShouldNotBeNull();
		withResolver.Metadata.ShouldBe(withoutResolver.Metadata);
		withResolver.EventType.ShouldBe(withoutResolver.EventType);
	}

	[Fact]
	public async Task Write_PayloadsTheReflectionPathReadsBack()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var expected = CreateEvent(aggregateId);

		var stored = await AppendAndLoadAsync(CreateStore(ResolverTestEventContext.Default), aggregateId)
			.ConfigureAwait(false);

		// Read back through the canonical reflection options a differently-configured host would use.
		var roundTripped = JsonSerializer.Deserialize<ResolverTestEvent>(
			stored.EventData, EventSerializationDefaults.Canonical);

		roundTripped.ShouldNotBeNull();
		roundTripped.EventId.ShouldBe(expected.EventId);
		roundTripped.AggregateId.ShouldBe(aggregateId);
		roundTripped.Name.ShouldBe(expected.Name);
		roundTripped.Shade.ShouldBe(expected.Shade);

		var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
			stored.Metadata!, EventSerializationDefaults.Canonical);

		metadata.ShouldNotBeNull();
		metadata["UserId"].GetString().ShouldBe("u-1");
		metadata["Attempt"].GetInt32().ShouldBe(2);
		metadata["Replayed"].GetBoolean().ShouldBeTrue();
		metadata["Absent"].ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public async Task Honour_TheConfiguredResolver_WhenResolvedFromTheContainer()
	{
		// The consumer composition: register the store, configure the resolver, resolve the contract.
		var services = new ServiceCollection();
		_ = services.AddInMemoryEventStore();
		_ = services.Configure<InMemoryEventStoreOptions>(
			static options => options.EventTypeInfoResolver = ResolverTestEventContext.Default);

		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var aggregateId = Guid.NewGuid().ToString();

		// A declared event round-trips.
		var result = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue();

		// And the container-resolved store is the one honouring the configured resolver, not a store that
		// happens to serialize everything through reflection.
		_ = await Should.ThrowAsync<NotSupportedException>(
			async () => await store.AppendAsync(
				Guid.NewGuid().ToString(),
				AggregateType,
				[new UndeclaredTestEvent { AggregateId = Guid.NewGuid().ToString() }],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	private static InMemoryEventStore CreateStore(IJsonTypeInfoResolver? resolver)
		=> new(
			UntenantedContext.Instance,
			Options.Create(new InMemoryEventStoreOptions { EventTypeInfoResolver = resolver }));

	private static ResolverTestEvent CreateEvent(string aggregateId) => new()
	{
		EventId = "e-1",
		AggregateId = aggregateId,
		OccurredAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
		Name = "order-placed",
		Shade = ResolverTestShade.Green,
		Metadata = new Dictionary<string, object>
		{
			["UserId"] = "u-1",
			["Attempt"] = 2,
			["Replayed"] = true,
			["Absent"] = null!,
		},
	};

	private static async Task<StoredEvent> AppendAndLoadAsync(InMemoryEventStore store, string aggregateId)
	{
		_ = await store.AppendAsync(
			aggregateId,
			AggregateType,
			[CreateEvent(aggregateId)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		return loaded.ShouldHaveSingleItem();
	}
}

internal enum ResolverTestShade
{
	Red,
	Green,
}

internal sealed class ResolverTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(ResolverTestEvent);

	public string Name { get; set; } = string.Empty;

	public ResolverTestShade Shade { get; set; }

	public IDictionary<string, object>? Metadata { get; set; }
}

internal sealed class UndeclaredTestEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(UndeclaredTestEvent);

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
[JsonSerializable(typeof(ResolverTestEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class ResolverTestEventContext : JsonSerializerContext;
