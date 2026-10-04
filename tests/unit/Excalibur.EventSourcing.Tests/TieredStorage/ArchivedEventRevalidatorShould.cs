// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ArchivedEventRevalidatorShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");
	private static StoredEvent Marker() => new("event-1", "aggregate", "Order", "Created", null, [9], 0, DateTimeOffset.UnixEpoch)
	{
		TenantId = Tenant.TenantId,
		GlobalPosition = 42,
		ArchivedAt = DateTimeOffset.UnixEpoch.AddDays(1),
	};

	private static EventStoreEventState State(StoredEvent row) => new(Tenant, row.AggregateId, row.AggregateType,
		row.EventId, row.Version, row.GlobalPosition, row.EventType, row.Timestamp, row.ArchivedAt);

	private static ValueTask<StoredEvent> ResolveAsync(StoredEvent original, IReadOnlyList<StoredEvent> cold,
		IEventStoreAuthoritativeReader reader, CancellationToken cancellationToken) =>
		new ArchivedEventRevalidator(Tenant, "aggregate", "Order", cold, reader).ResolveAsync(original, cancellationToken);

	[Fact]
	public async Task ObserveErasureCommittedAfterColdFetchAndDiscardStaleMetadata()
	{
		var hot = Marker();
		var state = State(hot);
		var reader = new Reader(() => state);
		// ColdFetch completes while the original event is live; Erase commits before Recheck.
		IReadOnlyList<StoredEvent> fetched = [hot with { EventData = [7] }];
		state = state with { EventType = ErasedEventMarker.EventType };
		var result = await ResolveAsync(hot, fetched, reader, CancellationToken.None);
		result.EventType.ShouldBe(ErasedEventMarker.EventType);
		result.EventData.ShouldBeNull();
		result.Metadata.ShouldBeNull();
		result.EventId.ShouldBe(hot.EventId);
		result.GlobalPosition.ShouldBe(42);
		result.Version.ShouldBe(0);
		reader.Calls.ShouldBe(1);
	}

	[Fact]
	public async Task AcceptFreshErasureWithoutAColdMatch()
	{
		var hot = Marker();
		var reader = new Reader(() => State(hot) with { EventType = ErasedEventMarker.EventType, ArchivedAt = null });
		var result = await ResolveAsync(hot, [], reader, CancellationToken.None);
		result.EventData.ShouldBeNull();
		result.Metadata.ShouldBeNull();
		result.ArchivedAt.ShouldBeNull();
	}

	[Fact]
	public async Task RestoreLivePayloadWhileRetainingHotProvenance()
	{
		var hot = Marker();
		var reader = new Reader(() => State(hot));
		var result = await ResolveAsync(hot,
			[hot with { EventData = [7], Metadata = [9], GlobalPosition = 0 }], reader, CancellationToken.None);
		result.EventData.ShouldBe(new byte[] { 7 });
		result.GlobalPosition.ShouldBe(42);
		result.Metadata.ShouldBeSameAs(hot.Metadata);
		result.TenantId.ShouldBe(Tenant.TenantId);
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("tenant")]
	[InlineData("id")]
	[InlineData("version")]
	[InlineData("position")]
	[InlineData("timestamp")]
	[InlineData("type")]
	[InlineData("aggregate-id")]
	[InlineData("aggregate-type")]
	[InlineData("archive-stamp")]
	[InlineData("position-unknown")]
	public async Task RefuseMissingOrChangedAuthoritativeState(string change)
	{
		var hot = Marker();
		var state = State(hot);
		var reader = new Reader(() => change switch
		{
			"missing" => null,
			"tenant" => state with { Tenant = KeyedTenantPartition.Scoped("tenant-b") },
			"id" => state with { EventId = "replacement", EventType = ErasedEventMarker.EventType },
			"version" => state with { Version = 1 },
			"position" => state with { GlobalPosition = 43 },
			"timestamp" => state with { Timestamp = state.Timestamp.AddSeconds(1) },
			"aggregate-id" => state with { AggregateId = "other" },
			"aggregate-type" => state with { AggregateType = "Other" },
			"archive-stamp" => state with { ArchivedAt = null },
			"position-unknown" => state with { GlobalPosition = 0 },
			_ => state with { EventType = "Different" },
		});
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await ResolveAsync(hot, [hot with { EventData = [7] }], reader, CancellationToken.None));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectMissingOrDuplicateColdIdentityForLiveState(bool duplicate)
	{
		var hot = Marker();
		var cold = hot with { EventData = [7] };
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await ResolveAsync(hot, duplicate ? [cold, cold] : [],
				new Reader(() => State(hot)), CancellationToken.None));
	}

	[Fact]
	public async Task DeferInvalidColdValidationForErasureButRejectItForTheNextLiveRead()
	{
		var hot = Marker();
		var live = hot with { EventId = "event-2", Version = 1, GlobalPosition = 43 };
		var reader = new BatchReader([State(hot) with { EventType = ErasedEventMarker.EventType }, State(live)]);
		var revalidator = new ArchivedEventRevalidator(Tenant, "aggregate", "Order",
			[hot, hot, live with { EventData = [2] }], reader);
		var erased = await revalidator.ResolveAsync(hot, CancellationToken.None);
		erased.EventType.ShouldBe(ErasedEventMarker.EventType);
		erased.Metadata.ShouldBeNull();
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await revalidator.ResolveAsync(live, CancellationToken.None));
		reader.ObservedVersions.ShouldBe(new long[] { 0, 1 });
	}

	[Fact]
	public async Task IsolateTheCachedSnapshotFromInputAndResultMutation()
	{
		var hot = Marker();
		var cold = hot with { EventData = [7], Metadata = [9] };
		var reader = new Reader(() => State(hot));
		var revalidator = new ArchivedEventRevalidator(Tenant, "aggregate", "Order", [cold], reader);
		cold.EventData![0] = 99;
		cold.Metadata![0] = 99;
		var first = await revalidator.ResolveAsync(hot, CancellationToken.None);
		first.EventData.ShouldBe(new byte[] { 7 });
		first.EventData![0] = 88;
		var second = await revalidator.ResolveAsync(hot, CancellationToken.None);
		second.EventData.ShouldBe(new byte[] { 7 });
		second.EventData.ShouldNotBeSameAs(first.EventData);
		reader.Calls.ShouldBe(2);
	}

	[Fact]
	public async Task RecheckEachEventWhileSharingTheColdIndex()
	{
		var first = Marker();
		var second = first with { EventId = "event-2", Version = 1, GlobalPosition = 43 };
		var reader = new BatchReader([State(first), State(second)]);
		var revalidator = new ArchivedEventRevalidator(Tenant, "aggregate", "Order",
			[first with { EventData = [1] }, second with { EventData = [2] }], reader);
		(await revalidator.ResolveAsync(first, CancellationToken.None)).EventData.ShouldBe(new byte[] { 1 });
		(await revalidator.ResolveAsync(second, CancellationToken.None)).EventData.ShouldBe(new byte[] { 2 });
		reader.ObservedVersions.ShouldBe(new long[] { 0, 1 });
	}

	[Theory]
	[InlineData("tenant")]
	[InlineData("aggregate-type")]
	[InlineData("duplicate-id")]
	[InlineData("id")]
	[InlineData("event-type")]
	[InlineData("timestamp")]
	[InlineData("position")]
	[InlineData("metadata")]
	[InlineData("metadata-null")]
	public async Task RefuseInvalidColdBatchOrMismatchedColdEvent(string defect)
	{
		var hot = Marker();
		var cold = hot with { EventData = [7] };
		var changed = defect switch
		{
			"tenant" => cold with { TenantId = "tenant-b" },
			"aggregate-type" => cold with { AggregateType = "Other" },
			"id" => cold with { EventId = "other" },
			"event-type" => cold with { EventType = "Other" },
			"timestamp" => cold with { Timestamp = cold.Timestamp.AddSeconds(1) },
			"position" => cold with { GlobalPosition = 43 },
			"metadata" => cold with { Metadata = [8] },
			"metadata-null" => cold with { Metadata = null },
			_ => cold with { Version = 1 },
		};
		IReadOnlyList<StoredEvent> batch = defect == "duplicate-id" ? [cold, changed] : [changed];
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await ResolveAsync(hot, batch, new Reader(() => State(hot)), CancellationToken.None));
	}

	[Fact]
	public async Task HonorCancellationDuringRecheckWithoutPoisoningLaterCalls()
	{
		var hot = Marker();
		using var cancellation = new CancellationTokenSource();
		var cancel = true;
		var reader = new Reader(() =>
		{
			if (cancel)
			{
				cancellation.Cancel();
			}
			return State(hot) with { EventType = ErasedEventMarker.EventType };
		});
		var revalidator = new ArchivedEventRevalidator(Tenant, "aggregate", "Order", [], reader);
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await revalidator.ResolveAsync(hot, cancellation.Token));
		cancel = false;
		(await revalidator.ResolveAsync(hot, CancellationToken.None)).EventType.ShouldBe(ErasedEventMarker.EventType);
		reader.Calls.ShouldBe(2);
	}

	[Fact]
	public async Task RefuseUnknownTenantBeforeInvokingReader()
	{
		var reader = new Reader(() => throw new InvalidOperationException("Must not query an inferred tenant."));
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await ResolveAsync(Marker() with { TenantId = null }, [], reader, CancellationToken.None));
		reader.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task HonorCancellationBeforeRecheck()
	{
		var reader = new Reader(() => State(Marker()));
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await ResolveAsync(Marker(), [], reader, cancellation.Token));
		reader.Calls.ShouldBe(0);
	}

	private sealed class BatchReader(IReadOnlyList<EventStoreEventState> states) : IEventStoreAuthoritativeReader
	{
		internal List<long> ObservedVersions { get; } = [];
		public ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant, string aggregateId,
			string aggregateType, string eventId, long version, CancellationToken cancellationToken)
		{
			ObservedVersions.Add(version);
			return ValueTask.FromResult<EventStoreEventState?>(states.Single(s => s.EventId == eventId && s.Version == version));
		}
	}

	private sealed class Reader(Func<EventStoreEventState?> read) : IEventStoreAuthoritativeReader
	{
		internal int Calls { get; private set; }
		public ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant, string aggregateId,
			string aggregateType, string eventId, long version, CancellationToken cancellationToken)
		{
			tenant.TenantId.ShouldBe(Tenant.TenantId);
			aggregateId.ShouldBe("aggregate");
			aggregateType.ShouldBe("Order");
			eventId.ShouldBe("event-1");
			version.ShouldBe(0);
			Calls++;
			return ValueTask.FromResult(read());
		}
	}
}
