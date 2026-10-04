// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class TieredGlobalStreamQueryShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelegateTheSelectedReadWithTheExactCursorLimitAndToken(bool filtered)
    {
        var fixture = new Fixture([Marker("A", "Order", 0, 8)]);
        var position = new GlobalStreamPosition(7, DateTimeOffset.UnixEpoch);
        using var cancellation = new CancellationTokenSource();
        if (filtered)
        {
            await fixture.Query.ReadByEventTypeAsync("Created", position, 11, cancellation.Token);
            A.CallTo(() => fixture.Inner.ReadByEventTypeAsync("Created", position, 11, cancellation.Token))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => fixture.Inner.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
        else
        {
            await fixture.Query.ReadAllAsync(position, 11, cancellation.Token);
            A.CallTo(() => fixture.Inner.ReadAllAsync(position, 11, cancellation.Token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fixture.Inner.ReadByEventTypeAsync(A<string>._, A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveTenantTypeIdentityAndGlobalOrderWithPerPageColdCaching(bool filtered)
    {
        StoredEvent[] page = [Marker("A", "Order", 0, 1), Marker("a", "Order", 0, 2),
            Marker("A", "order", 0, 3), Marker("A", "Order", 1, 4)];
        var fixture = new Fixture(page);
        var result = await fixture.Read(filtered);
        result.Select(e => e.EventId).ShouldBe(page.Select(e => e.EventId));
        result.Select(e => e.GlobalPosition).ShouldBe(new long[] { 1, 2, 3, 4 });
        foreach (var row in result)
        {
            row.EventData.ShouldBe(new[] { (byte)row.GlobalPosition });
            row.Metadata.ShouldBe(new byte[] { 9 });
        }
        fixture.ColdReads.Count.ShouldBe(3);
        fixture.Reader.Observed.ShouldBe(page.Select(e => e.EventId));
        await fixture.Read(filtered);
        fixture.ColdReads.Count.ShouldBe(6, "cold data must not be cached across pages");
        fixture.Reader.Observed.Count.ShouldBe(8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecheckEachMarkerEvenWhenItsStreamWasAlreadyFetched(bool filtered)
    {
        StoredEvent[] page = [Marker("A", "Order", 0, 1), Marker("A", "Order", 1, 2)];
        var fixture = new Fixture(page);
        var erased = false;
        fixture.Reader.Observe = id =>
        {
            var state = State(page.Single(e => e.EventId == id));
            if (id == page[0].EventId)
            {
                // Erasure commits after the first event's observation but before the second's.
                erased = true;
                return state;
            }
            return state with { EventType = erased ? ErasedEventMarker.EventType : state.EventType };
        };
        var result = await fixture.Read(filtered);
        result[0].EventData.ShouldNotBeNull();
        result[1].EventType.ShouldBe(ErasedEventMarker.EventType);
        result[1].EventData.ShouldBeNull();
        result[1].Metadata.ShouldBeNull();
        result.Select(e => e.GlobalPosition).ShouldBe(new long[] { 1, 2 });
        fixture.ColdReads.Count.ShouldBe(1);
        fixture.Reader.Observed.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FailTheWholePageWhenALaterStreamFailsOrCancels(bool filtered, bool cancel)
    {
        StoredEvent[] page = [Marker("A", "Order", 0, 1), Marker("B", "Order", 0, 2)];
        var fixture = new Fixture(page);
        using var cancellation = new CancellationTokenSource();
        fixture.BeforeCold = tenant =>
        {
            if (tenant == "B")
            {
                if (cancel)
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                }
                throw new InvalidOperationException("cold unavailable");
            }
        };
        IReadOnlyList<StoredEvent>? delivered = null;
        if (cancel)
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => delivered = await fixture.Read(filtered, cancellation.Token));
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => delivered = await fixture.Read(filtered));
        }
        delivered.ShouldBeNull();
        fixture.Reader.Observed.ShouldBe(new[] { page[0].EventId });
    }

    [Theory]
    [InlineData("null-tenant")]
    [InlineData("empty-tenant")]
    [InlineData("space-tenant")]
    [InlineData("negative-version")]
    [InlineData("zero-position")]
    [InlineData("duplicate-position")]
    [InlineData("unlocated-payload")]
    public async Task RejectMalformedLaterRowsBeforeAnyColdAccess(string defect)
    {
        var later = Marker("B", "Order", 0, 2);
        later = defect switch
        {
            "null-tenant" => later with { TenantId = null },
            "empty-tenant" => later with { TenantId = "" },
            "space-tenant" => later with { TenantId = " " },
            "negative-version" => later with { Version = -1 },
            "zero-position" => later with { GlobalPosition = 0 },
            "duplicate-position" => later with { GlobalPosition = 1 },
            _ => later with { ArchivedAt = null },
        };
        var fixture = new Fixture([Marker("A", "Order", 0, 1), later]);
        await Should.ThrowAsync<InvalidOperationException>(async () => await fixture.Read(false));
        fixture.ColdReads.ShouldBeEmpty();
        fixture.Reader.Observed.ShouldBeEmpty();
    }

    [Fact]
    public async Task FilterTheContiguousPrefixBeforeAccessingAnUnavailableArchiveAboveTheGap()
    {
        StoredEvent[] page = [Marker("A", "Order", 0, 1) with { EventData = [1] }, Marker("B", "Order", 0, 3)];
        var fixture = new Fixture(page, contiguous: true);
        fixture.BeforeCold = _ => throw new InvalidOperationException("must not fetch above gap");
        var result = await fixture.Read(false);
        result.Count.ShouldBe(1);
        result[0].ShouldBeSameAs(page[0]);
        fixture.ColdReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateHeadAndLeaveReadableOrAlreadyErasedRowsOnTheHotPath()
    {
        StoredEvent[] page = [Marker("A", "Order", 0, 1) with { EventData = [3] },
            Marker("B", "Order", 0, 2) with { EventType = ErasedEventMarker.EventType }];
        var fixture = new Fixture(page);
        A.CallTo(() => fixture.Inner.GetHeadPositionAsync(A<CancellationToken>._)).Returns(42L);
        (await fixture.Query.GetHeadPositionAsync(CancellationToken.None)).ShouldBe(42L);
        var result = await fixture.Read(false);
        result[0].ShouldBeSameAs(page[0]);
        result[1].ShouldBeSameAs(page[1]);
        fixture.ColdReads.ShouldBeEmpty();
        fixture.Reader.Observed.ShouldBeEmpty();
    }

    private static StoredEvent Marker(string tenant, string type, long version, long position) =>
        new($"{tenant}-{type}-{version}", "shared-id", type, "Created", null, [9], version, DateTimeOffset.UnixEpoch)
        {
            TenantId = tenant,
            GlobalPosition = position,
            ArchivedAt = DateTimeOffset.UnixEpoch.AddDays(1),
        };

    private static EventStoreEventState State(StoredEvent row) => new(
        KeyedTenantPartition.FromStoredValue(row.TenantId!), row.AggregateId, row.AggregateType, row.EventId,
        row.Version, row.GlobalPosition, row.EventType, row.Timestamp, row.ArchivedAt);

    private sealed class Fixture
    {
        public Fixture(StoredEvent[] page, bool contiguous = false)
        {
            Reader = new Reader { Observe = id => State(page.Single(e => e.EventId == id)) };
            A.CallTo(() => Inner.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._)).Returns(page);
            A.CallTo(() => Inner.ReadByEventTypeAsync("Created", A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._)).Returns(page);
            var cold = A.Fake<IColdEventStore>();
            A.CallTo(() => cold.ReadAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<CancellationToken>._))
                .ReturnsLazily(call =>
                {
                    var tenant = call.GetArgument<KeyedTenantPartition>(0)!.TenantId;
                    var id = call.GetArgument<string>(1)!;
                    var type = call.GetArgument<string>(2)!;
                    ColdReads.Add((tenant, type, id));
                    BeforeCold?.Invoke(tenant);
                    return Task.FromResult<IReadOnlyList<StoredEvent>>(page
                        .Where(e => e.TenantId == tenant && e.AggregateType == type && e.AggregateId == id)
                        .Select(e => e with { EventData = [(byte)e.GlobalPosition] }).ToArray());
                });
            Query = new TieredGlobalStreamQuery(contiguous
                ? new ContiguousGlobalStreamQuery(Inner, NullLogger<ContiguousGlobalStreamQuery>.Instance) : Inner, cold, Reader);
        }

        public IGlobalStreamQuery Inner { get; } = A.Fake<IGlobalStreamQuery>();
        public TieredGlobalStreamQuery Query { get; }
        public Reader Reader { get; }
        public List<(string Tenant, string Type, string Id)> ColdReads { get; } = [];
        public Action<string>? BeforeCold { get; set; }
        public ValueTask<IReadOnlyList<StoredEvent>> Read(bool filtered, CancellationToken token = default) => filtered
            ? Query.ReadByEventTypeAsync("Created", GlobalStreamPosition.Start, 100, token)
            : Query.ReadAllAsync(GlobalStreamPosition.Start, 100, token);
    }

    private sealed class Reader : IEventStoreAuthoritativeReader
    {
        public required Func<string, EventStoreEventState?> Observe { get; set; }
        public List<string> Observed { get; } = [];
        public ValueTask<EventStoreEventState?> ReadCurrentAsync(KeyedTenantPartition tenant, string aggregateId,
            string aggregateType, string eventId, long version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observed.Add(eventId);
            return ValueTask.FromResult(Observe(eventId));
        }
    }
}
