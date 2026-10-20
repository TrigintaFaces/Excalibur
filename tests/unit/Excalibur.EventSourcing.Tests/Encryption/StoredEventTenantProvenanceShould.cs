// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Data.CloudNative;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.DynamoDb;
using Excalibur.EventSourcing.Firestore;

namespace Excalibur.EventSourcing.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class StoredEventTenantProvenanceShould
{
    private static readonly Type[] Providers =
        [typeof(CosmosDbEventStore), typeof(DynamoDbEventStore), typeof(FirestoreEventStore)];

    [Theory]
    [InlineData("t:tenant-a:Order:agg-1", "tenant-a")]
    [InlineData("t:tenant%3Aa%25é:Order:agg-1", "tenant:a%é")]
    [InlineData("t:__default__:Order:agg-1", "__default__")]
    public void PreserveTheOwnerFromTheStoredPartition(string partitionKey, string expectedTenant)
    {
        foreach (var provider in Providers)
        {
            var cloud = Event(partitionKey);
            var mapped = (StoredEvent)Mapper(provider).Invoke(null, [cloud])!;
            mapped.TenantId.ShouldBe(expectedTenant);
            mapped.AggregateId.ShouldBe(cloud.AggregateId);
            mapped.AggregateType.ShouldBe(cloud.AggregateType);
            mapped.EventData.ShouldBeSameAs(cloud.EventData);
        }
    }

    [Theory]
    [InlineData("x:tenant-a:Order:agg-1")]
    [InlineData("t::Order:agg-1")]
    [InlineData("t: :Order:agg-1")]
    [InlineData("t:tenant-a:Other:agg-1")]
    [InlineData("t:tenant-a:Order:other")]
    public void RefuseMissingOrInconsistentPartitionOwnership(string partitionKey)
    {
        foreach (var provider in Providers)
        {
            var error = Should.Throw<TargetInvocationException>(() => Mapper(provider).Invoke(null, [Event(partitionKey)]));
            error.InnerException.ShouldBeOfType<InvalidOperationException>();
        }
    }

    private static MethodInfo Mapper(Type provider) =>
        provider.GetMethod("ToStoredEvent", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The provider's stored-event mapper was not found.");

    private static CloudStoredEvent Event(string partitionKey) => new()
    {
        EventId = "evt-1",
        AggregateId = "agg-1",
        AggregateType = "Order",
        EventType = "OrderPlaced",
        EventData = [1, 2, 3],
        Version = 1,
        Timestamp = DateTimeOffset.UnixEpoch,
        PartitionKeyValue = partitionKey,
    };
}
