// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class TieredAuthoritativeHydrationShould
{
	private readonly IEventStore _hot = A.Fake<IEventStore>();
	private readonly IColdEventStore _cold = A.Fake<IColdEventStore>();
	private readonly Context _context = new();
	private readonly Reader _reader;
	private readonly TieredEventStoreDecorator _store;
	private readonly StoredEvent[] _markers = [Marker(0), Marker(1)];

	public TieredAuthoritativeHydrationShould()
	{
		_reader = new Reader(_context) { Observe = v => State(_markers.Single(e => e.Version == v)) };
		A.CallTo(() => _hot.GetService(typeof(IEventStoreAuthoritativeReader))).Returns(_reader);
		A.CallTo(() => _hot.LoadAsync("aggregate", "Order", A<CancellationToken>._)).Returns(_markers);
		A.CallTo(() => _hot.LoadAsync("aggregate", "Order", 0L, A<CancellationToken>._)).Returns(new[] { _markers[1] });
		A.CallTo(() => _cold.ReadAsync(A<KeyedTenantPartition>._, "aggregate", "Order", A<CancellationToken>._))
			.Returns(_markers.Select(e => e with { EventData = [7] }).ToArray());
		_store = Create(_hot);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ObserveErasureCommittedDuringColdFetchWithoutChangingMembership(bool fromVersion)
	{
		var erased = false;
		_reader.Observe = v => State(_markers.Single(e => e.Version == v)) with
		{
			EventType = erased && v == 1 ? ErasedEventMarker.EventType : "Created",
		};
		A.CallTo(() => _cold.ReadAsync(A<KeyedTenantPartition>._, "aggregate", "Order", A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				// HotRead returned live archive markers; Erase commits before ColdFetch returns.
				erased = true;
				return Task.FromResult<IReadOnlyList<StoredEvent>>(_markers.Select(e => e with { EventData = [7] }).ToArray());
			});
		var result = fromVersion
			? await _store.LoadAsync("aggregate", "Order", 0, CancellationToken.None)
			: await _store.LoadAsync("aggregate", "Order", CancellationToken.None);
		result.Select(e => e.Version).ShouldBe(fromVersion ? new long[] { 1 } : new long[] { 0, 1 });
		result.Select(e => e.GlobalPosition).ShouldBe(fromVersion ? new long[] { 101 } : new long[] { 100, 101 });
		result[^1].EventType.ShouldBe(ErasedEventMarker.EventType);
		result[^1].EventData.ShouldBeNull();
		result[^1].Metadata.ShouldBeNull();
		if (!fromVersion)
		{
			result[0].EventData.ShouldBe(new byte[] { 7 });
		}
		_reader.Versions.ShouldBe(fromVersion ? new long[] { 1 } : new long[] { 0, 1 });
	}

	[Fact]
	public async Task RejectTenantChangeDuringColdFetchThroughTheCachedCapability()
	{
		A.CallTo(() => _cold.ReadAsync(A<KeyedTenantPartition>._, "aggregate", "Order", A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_context.TenantId = "tenant-b";
				return Task.FromResult<IReadOnlyList<StoredEvent>>(_markers.Select(e => e with { EventData = [7] }).ToArray());
			});
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _store.LoadAsync("aggregate", "Order", CancellationToken.None));
		_reader.Versions.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverReturnAPartialListWhenALaterObservationFails(bool cancellation)
	{
		using var source = new CancellationTokenSource();
		_reader.Observe = v =>
		{
			if (v == 1)
			{
				if (cancellation)
				{
					source.Cancel();
				}
				else
				{
					throw new InvalidOperationException("authoritative read unavailable");
				}
			}
			return State(_markers.Single(e => e.Version == v));
		};
		IReadOnlyList<StoredEvent>? result = null;
		if (cancellation)
		{
			await Should.ThrowAsync<OperationCanceledException>(async () =>
				result = await _store.LoadAsync("aggregate", "Order", source.Token));
		}
		else
		{
			await Should.ThrowAsync<InvalidOperationException>(async () =>
				result = await _store.LoadAsync("aggregate", "Order", source.Token));
		}
		_reader.Versions.ShouldBe(new long[] { 0, 1 });
		result.ShouldBeNull();
	}

	[Fact]
	public void RefuseMissingOrDeliberatelyHiddenCapabilities()
	{
		Should.Throw<InvalidOperationException>(() => Create(A.Fake<IEventStore>()));
		Should.Throw<InvalidOperationException>(() => Create(new DenyingDecorator(_hot)));
		A.CallTo(() => _hot.GetService(typeof(IEventStoreAuthoritativeReader))).MustHaveHappenedOnceExactly();
	}

	private TieredEventStoreDecorator Create(IEventStore hot) => new(hot, _cold,
		NullLogger<TieredEventStoreDecorator>.Instance, _context);

	private static StoredEvent Marker(long version) => new($"event-{version}", "aggregate", "Order", "Created",
		null, [9], version, DateTimeOffset.UnixEpoch)
	{
		TenantId = "tenant-a", GlobalPosition = 100 + version, ArchivedAt = DateTimeOffset.UnixEpoch.AddDays(1),
	};

	private static EventStoreEventState State(StoredEvent row) => new(KeyedTenantPartition.FromStoredValue(row.TenantId!),
		row.AggregateId, row.AggregateType, row.EventId, row.Version, row.GlobalPosition, row.EventType, row.Timestamp, row.ArchivedAt);

	private sealed class Context : ITenantContext
	{
		public string? TenantId { get; set; } = "tenant-a";
		public bool HasTenant => TenantId is not null;
	}

	private sealed class Reader(Context context) : IEventStoreAuthoritativeReader
	{
		public required Func<long, EventStoreEventState?> Observe { get; set; }
		public List<long> Versions { get; } = [];
		public ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant, string aggregateId,
			string aggregateType, string eventId, long version, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (KeyedTenantPartition.FromContext(context).TenantId != tenant.TenantId)
			{
				throw new InvalidOperationException("tenant scope changed");
			}
			Versions.Add(version);
			return ValueTask.FromResult(Observe(version));
		}
	}

	private sealed class DenyingDecorator(IEventStore inner) : IsolatingEventStoreDecorator(inner);
}
