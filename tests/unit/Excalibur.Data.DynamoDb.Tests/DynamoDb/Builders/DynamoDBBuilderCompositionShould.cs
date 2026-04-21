// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Data.Tests.DynamoDb.Builders;

/// <summary>
/// Composition tests verifying that all DynamoDB builders share the same
/// canonical connection overload pattern and last-wins semantics.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDBBuilderCompositionShould : UnitTestBase
{
    // --- All 5 canonical builders implement the same interface shape ---

    [Fact]
    public void DataBuilder_ImplementIDynamoDBDataBuilder()
    {
        IDynamoDBDataBuilder builder = new DynamoDBDataBuilder();
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void EventSourcingBuilder_ImplementIDynamoDBEventSourcingBuilder()
    {
        IDynamoDBEventSourcingBuilder builder = new DynamoDBEventSourcingBuilder();
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void SagaBuilder_ImplementIDynamoDBSagaBuilder()
    {
        IDynamoDBSagaBuilder builder = new DynamoDBSagaBuilder();
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void InboxBuilder_ImplementIDynamoDBInboxBuilder()
    {
        IDynamoDBInboxBuilder builder = new DynamoDBInboxBuilder();
        builder.ShouldNotBeNull();
    }

    [Fact]
    public void OutboxBuilder_ImplementIDynamoDBOutboxBuilder()
    {
        IDynamoDBOutboxBuilder builder = new DynamoDBOutboxBuilder();
        builder.ShouldNotBeNull();
    }

    // --- All canonical builders have the same 5 connection overloads + 2 feature methods ---

    [Fact]
    public void AllCanonicalBuilders_HaveConsistentConnectionOverloads()
    {
        // Verify that each builder type has ServiceUrl, Region, Client, ClientFactory, BindConfiguration
        // by exercising them via the public interface.
        var client = A.Fake<IAmazonDynamoDB>();
        Func<IServiceProvider, IAmazonDynamoDB> factory = _ => client;

        // Data
        var data = new DynamoDBDataBuilder();
        data.ServiceUrl("http://localhost:8000").Region(RegionEndpoint.USEast1)
            .Client(client).ClientFactory(factory).BindConfiguration("a:b")
            .TableName("t").TablePrefix("p");

        // EventSourcing
        var es = new DynamoDBEventSourcingBuilder();
        es.ServiceUrl("http://localhost:8000").Region(RegionEndpoint.USEast1)
            .Client(client).ClientFactory(factory).BindConfiguration("a:b")
            .TableName("t").TablePrefix("p");

        // Saga
        var saga = new DynamoDBSagaBuilder();
        saga.ServiceUrl("http://localhost:8000").Region(RegionEndpoint.USEast1)
            .Client(client).ClientFactory(factory).BindConfiguration("a:b")
            .TableName("t").TablePrefix("p");

        // Inbox
        var inbox = new DynamoDBInboxBuilder();
        inbox.ServiceUrl("http://localhost:8000").Region(RegionEndpoint.USEast1)
            .Client(client).ClientFactory(factory).BindConfiguration("a:b")
            .TableName("t").TablePrefix("p");

        // Outbox
        var outbox = new DynamoDBOutboxBuilder();
        outbox.ServiceUrl("http://localhost:8000").Region(RegionEndpoint.USEast1)
            .Client(client).ClientFactory(factory).BindConfiguration("a:b")
            .TableName("t").TablePrefix("p");

        // After full chain, last-wins means BindConfiguration is active for all
        data.BindConfigurationPath.ShouldBe("a:b");
        es.BindConfigurationPath.ShouldBe("a:b");
        saga.BindConfigurationPath.ShouldBe("a:b");
        inbox.BindConfigurationPath.ShouldBe("a:b");
        outbox.BindConfigurationPath.ShouldBe("a:b");

        // TableName and TablePrefix survive because they don't clear connection state
        data.TableNameValue.ShouldBe("t");
        es.TablePrefixValue.ShouldBe("p");
        saga.TableNameValue.ShouldBe("t");
        inbox.TablePrefixValue.ShouldBe("p");
        outbox.TableNameValue.ShouldBe("t");
    }

    // --- CDC builder has a distinct shape (not canonical) ---

    [Fact]
    public void CdcBuilder_HasDistinctApiShape()
    {
        var options = new DynamoDbCdcOptions();
        IDynamoDbCdcBuilder builder = new DynamoDbCdcBuilder(options);

        // CDC builder has TableName, StreamArn, ProcessorName, WithStateStore, BindConfiguration
        // but NOT ServiceUrl, Region, Client, ClientFactory (connection is managed differently)
        builder
            .TableName("orders")
            .StreamArn("arn:aws:dynamodb:us-east-1:123:table/orders/stream/2026")
            .ProcessorName("cdc-proc")
            .BindConfiguration("Cdc:DynamoDB");

        options.TableName.ShouldBe("orders");
        options.StreamArn.ShouldBe("arn:aws:dynamodb:us-east-1:123:table/orders/stream/2026");
        options.ProcessorName.ShouldBe("cdc-proc");
    }

    // --- CDC state store builder implements ICdcStateStoreBuilder ---

    [Fact]
    public void CdcStateStoreBuilder_ImplementICdcStateStoreBuilder()
    {
        var options = new DynamoDbCdcStateStoreOptions();
        ICdcStateStoreBuilder builder = new DynamoDbCdcStateStoreBuilder(options);
        builder.ShouldNotBeNull();
    }

    // --- Verify all canonical builders produce consistent last-wins ---

    [Fact]
    public void AllCanonicalBuilders_ClientClearsServiceUrl()
    {
        var client = A.Fake<IAmazonDynamoDB>();

        var data = new DynamoDBDataBuilder();
        data.ServiceUrl("http://localhost:8000");
        data.Client(client);
        data.ServiceUrlValue.ShouldBeNull();

        var es = new DynamoDBEventSourcingBuilder();
        es.ServiceUrl("http://localhost:8000");
        es.Client(client);
        es.ServiceUrlValue.ShouldBeNull();

        var saga = new DynamoDBSagaBuilder();
        saga.ServiceUrl("http://localhost:8000");
        saga.Client(client);
        saga.ServiceUrlValue.ShouldBeNull();

        var inbox = new DynamoDBInboxBuilder();
        inbox.ServiceUrl("http://localhost:8000");
        inbox.Client(client);
        inbox.ServiceUrlValue.ShouldBeNull();

        var outbox = new DynamoDBOutboxBuilder();
        outbox.ServiceUrl("http://localhost:8000");
        outbox.Client(client);
        outbox.ServiceUrlValue.ShouldBeNull();
    }
}
