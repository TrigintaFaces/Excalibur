// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.MongoDB;

namespace Excalibur.Data.MongoDB.Tests.MongoDB.EventSourcing;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class MongoDbEventDocumentTenantShould
{
    [Theory]
    [InlineData("t:tenant-a:agg-1", "tenant-a")]
    [InlineData("t:tenant%3Aa%25é:agg-1", "tenant:a%é")]
    public void PreserveTheStoredOwner(string streamId, string expectedTenant)
    {
        var document = Document(streamId);
        var stored = document.ToStoredEvent("agg-1");
        stored.TenantId.ShouldBe(expectedTenant);
        stored.AggregateId.ShouldBe("agg-1");
        stored.EventData.ShouldBeSameAs(document.Payload);
    }

    [Theory]
    [InlineData("x:tenant-a:agg-1")]
    [InlineData("t::agg-1")]
    [InlineData("t: :agg-1")]
    [InlineData("t:tenant-a:other")]
    public void RefuseAnUnownedOrDifferentStream(string streamId) =>
        Should.Throw<InvalidOperationException>(() => Document(streamId).ToStoredEvent("agg-1"));

    private static MongoDbEventDocument Document(string streamId) => new()
    {
        StreamId = streamId,
        EventId = "evt-1",
        AggregateType = "Order",
        EventType = "OrderPlaced",
        Payload = [1, 2, 3],
        Version = 1,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };
}
