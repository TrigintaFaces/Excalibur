// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.MongoDB;
using Excalibur.Inbox.MongoDB;
using Excalibur.LeaderElection.MongoDB;
using Excalibur.Saga.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Builders;

/// <summary>
/// Multi-subsystem composition tests verifying that MongoDB builders for
/// different subsystems can coexist without DI or state conflicts.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class MongoDBCompositionShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    [Fact]
    public void Compose_FourBuildersWithSameClient_NoDiConflicts()
    {
        // Arrange
        var client = A.Fake<IMongoClient>();

        var esOptions = new MongoDbEventStoreOptions();
        var esBuilder = new MongoDBEventSourcingBuilder(esOptions);

        var sagaOptions = new MongoDbSagaOptions();
        var sagaBuilder = new MongoDBSagaBuilder(sagaOptions);

        var inboxOptions = new MongoDbInboxOptions();
        var inboxBuilder = new MongoDBInboxBuilder(inboxOptions);

        var leOptions = new MongoDbLeaderElectionOptions();
        var leBuilder = new MongoDBLeaderElectionBuilder(leOptions);

        // Act -- all four builders share the same IMongoClient
        esBuilder.Client(client);
        sagaBuilder.Client(client);
        inboxBuilder.Client(client);
        leBuilder.Client(client);

        // Assert -- each builder stores the same client independently
        esBuilder.ClientInstance.ShouldBe(client);
        sagaBuilder.ClientInstance.ShouldBe(client);
        inboxBuilder.ClientInstance.ShouldBe(client);
        leBuilder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void Compose_IndependentOptions_OptionsIsolation()
    {
        // Arrange
        var esOptions = new MongoDbEventStoreOptions();
        var esBuilder = new MongoDBEventSourcingBuilder(esOptions);

        var sagaOptions = new MongoDbSagaOptions();
        var sagaBuilder = new MongoDBSagaBuilder(sagaOptions);

        var inboxOptions = new MongoDbInboxOptions();
        var inboxBuilder = new MongoDBInboxBuilder(inboxOptions);

        var leOptions = new MongoDbLeaderElectionOptions();
        var leBuilder = new MongoDBLeaderElectionBuilder(leOptions);

        // Act -- set different DatabaseName per subsystem
        esBuilder.ConnectionString(TestConnectionString).DatabaseName("es_db");
        sagaBuilder.ConnectionString(TestConnectionString).DatabaseName("saga_db");
        inboxBuilder.ConnectionString(TestConnectionString).DatabaseName("inbox_db");
        leBuilder.ConnectionString(TestConnectionString).DatabaseName("le_db");

        // Assert -- each options instance is independently configured
        esOptions.DatabaseName.ShouldBe("es_db");
        sagaOptions.DatabaseName.ShouldBe("saga_db");
        inboxOptions.DatabaseName.ShouldBe("inbox_db");
        leOptions.DatabaseName.ShouldBe("le_db");
    }

    [Fact]
    public void Compose_RegistrationOrder_OrderIndependent()
    {
        // Arrange -- register in reverse order (LE, Inbox, Saga, ES)
        var leOptions = new MongoDbLeaderElectionOptions();
        var leBuilder = new MongoDBLeaderElectionBuilder(leOptions);
        leBuilder.ConnectionString(TestConnectionString).DatabaseName("le_db");

        var inboxOptions = new MongoDbInboxOptions();
        var inboxBuilder = new MongoDBInboxBuilder(inboxOptions);
        inboxBuilder.ConnectionString(TestConnectionString).DatabaseName("inbox_db");

        var sagaOptions = new MongoDbSagaOptions();
        var sagaBuilder = new MongoDBSagaBuilder(sagaOptions);
        sagaBuilder.ConnectionString(TestConnectionString).DatabaseName("saga_db");

        var esOptions = new MongoDbEventStoreOptions();
        var esBuilder = new MongoDBEventSourcingBuilder(esOptions);
        esBuilder.ConnectionString(TestConnectionString).DatabaseName("es_db");

        // Assert -- order of builder creation has no effect on stored state
        leOptions.ConnectionString.ShouldBe(TestConnectionString);
        leOptions.DatabaseName.ShouldBe("le_db");

        inboxOptions.ConnectionString.ShouldBe(TestConnectionString);
        inboxOptions.DatabaseName.ShouldBe("inbox_db");

        sagaOptions.ConnectionString.ShouldBe(TestConnectionString);
        sagaOptions.DatabaseName.ShouldBe("saga_db");

        esOptions.ConnectionString.ShouldBe(TestConnectionString);
        esOptions.DatabaseName.ShouldBe("es_db");
    }
}
