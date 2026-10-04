// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

internal sealed class HydratedConsumerFixture
{
    public HydratedConsumerFixture(bool corruptLaterArchive)
    {
        StoredEvent[] originals = [Create(1, 17), Create(2, 5), Create(3, 11), Create(4, 999), Create(5, 13)];
        var hot = originals.Select(e => e.GlobalPosition is 1 or 3 or 4
            ? e with { EventData = null, ArchivedAt = DateTimeOffset.UnixEpoch.AddDays(1) }
            : e).ToArray();
        var inner = A.Fake<IGlobalStreamQuery>();
        A.CallTo(() => inner.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                var cursor = call.GetArgument<GlobalStreamPosition>(0)!.Position;
                RequestedPositions.Add(cursor);
                OnRead?.Invoke(cursor);
                return new ValueTask<IReadOnlyList<StoredEvent>>(hot.Where(e => e.GlobalPosition > cursor)
                    .Take(call.GetArgument<int>(1)).ToArray());
            });
        A.CallTo(() => inner.GetHeadPositionAsync(A<CancellationToken>._)).Returns(5L);
        var cold = A.Fake<IColdEventStore>();
        A.CallTo(() => cold.ReadAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                call.GetArgument<KeyedTenantPartition>(0)!.TenantId.ShouldBe("tenant-A");
                call.GetArgument<string>(2).ShouldBe("Order");
                var id = call.GetArgument<string>(1)!;
                ColdReads.Add(id);
                var original = originals.Single(e => e.AggregateId == id);
                if (corruptLaterArchive && original.GlobalPosition == 3)
                {
                    original = original with { EventId = "wrong-event" };
                }
                return Task.FromResult<IReadOnlyList<StoredEvent>>([original]);
            });
        var reader = A.Fake<IEventStoreAuthoritativeReader>();
        A.CallTo(() => reader.ReadCurrentAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._,
                A<string>._, A<long>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                var row = hot.Single(e => e.EventId == call.GetArgument<string>(3));
                call.GetArgument<KeyedTenantPartition>(0)!.TenantId.ShouldBe(row.TenantId);
                call.GetArgument<string>(1).ShouldBe(row.AggregateId);
                call.GetArgument<string>(2).ShouldBe(row.AggregateType);
                call.GetArgument<long>(4).ShouldBe(row.Version);
                Observed.Add(row.EventId);
                return ValueTask.FromResult<EventStoreEventState?>(new EventStoreEventState(
                    KeyedTenantPartition.Scoped("tenant-A"), row.AggregateId, row.AggregateType, row.EventId,
                    row.Version, row.GlobalPosition, row.GlobalPosition == 4 ? ErasedEventMarker.EventType : row.EventType,
                    row.Timestamp, row.ArchivedAt));
            });
        Query = new TieredGlobalStreamQuery(inner, cold, reader);
        A.CallTo(() => Serializer.ResolveType("AmountAdded")).Returns(typeof(AmountAdded));
        A.CallTo(() => Serializer.DeserializeEvent(A<byte[]>._, typeof(AmountAdded)))
            .ReturnsLazily(call =>
            {
                var decoded = JsonSerializer.Deserialize<AmountAdded>(call.GetArgument<byte[]>(0)!)!;
                Decoded.Add(decoded.EventId);
                return decoded;
            });
    }

    public IGlobalStreamQuery Query { get; }
    public IEventSerializer Serializer { get; } = A.Fake<IEventSerializer>();
    public List<long> RequestedPositions { get; } = [];
    public List<string> ColdReads { get; } = [];
    public List<string> Observed { get; } = [];
    public List<string> Decoded { get; } = [];
    public Action<long>? OnRead { get; set; }

    private static StoredEvent Create(long position, int amount)
    {
        var message = new AmountAdded
        {
            EventId = $"event-{position}", AggregateId = $"order-{position}", Amount = amount,
        };
        return new StoredEvent(message.EventId, message.AggregateId, "Order", "AmountAdded",
            JsonSerializer.SerializeToUtf8Bytes(message), null, 0, DateTimeOffset.UnixEpoch)
        {
            TenantId = "tenant-A", GlobalPosition = position,
        };
    }

    [MessageName("Test.HydratedConsumer.AmountAdded")]
    internal sealed record AmountAdded : IDomainEvent
    {
        public required string EventId { get; init; }
        public required string AggregateId { get; init; }
        public int Amount { get; init; }
        public long Version { get; init; }
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;
        public IDictionary<string, object>? Metadata { get; init; }
    }
}
