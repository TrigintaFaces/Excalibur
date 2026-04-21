// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;
using Excalibur.Data.Firestore;
using Excalibur.EventSourcing.Firestore;
using Excalibur.Inbox.Firestore;
using Excalibur.Outbox.Firestore;
using Excalibur.Saga.Firestore;

using Google.Cloud.Firestore;

namespace Excalibur.Data.Tests.Firestore.Builders;

/// <summary>
/// Composition tests verifying that multiple Firestore builders can coexist
/// with isolated options and independent configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class FirestoreCompositionShould : UnitTestBase
{
    [Fact]
    public void MultipleBuilders_MaintainIsolatedOptions()
    {
        // Arrange
        var esOptions = new FirestoreEventStoreOptions();
        var esBuilder = new FirestoreEventSourcingBuilder(esOptions);

        var dataOptions = new FirestoreOptions();
        var dataBuilder = new FirestoreDataBuilder(dataOptions);

        var inboxOptions = new FirestoreInboxOptions();
        var inboxBuilder = new FirestoreInboxBuilder(inboxOptions);

        var outboxOptions = new FirestoreOutboxOptions();
        var outboxBuilder = new FirestoreOutboxBuilder(outboxOptions);

        var sagaOptions = new FirestoreSagaOptions();
        var sagaBuilder = new FirestoreSagaBuilder(sagaOptions);

        var cdcOptions = new FirestoreCdcOptions();
        var cdcBuilder = new FirestoreCdcBuilder(cdcOptions);

        // Act - configure each with different ProjectIds and CollectionNames
        esBuilder.ProjectId("es-project").CollectionName("events");
        dataBuilder.ProjectId("data-project").CollectionName("documents");
        inboxBuilder.ProjectId("inbox-project").CollectionName("inbox-messages");
        outboxBuilder.ProjectId("outbox-project").CollectionName("outbox-messages");
        sagaBuilder.ProjectId("saga-project").CollectionName("sagas");
        cdcBuilder.ProjectId("cdc-project").CollectionPath("cdc-changes");

        // Assert - each builder/options has its own values
        esBuilder.ProjectIdValue.ShouldBe("es-project");
        esOptions.EventsCollectionName.ShouldBe("events");

        dataBuilder.ProjectIdValue.ShouldBe("data-project");
        dataOptions.DefaultCollection.ShouldBe("documents");

        inboxBuilder.ProjectIdValue.ShouldBe("inbox-project");
        inboxOptions.CollectionName.ShouldBe("inbox-messages");

        outboxBuilder.ProjectIdValue.ShouldBe("outbox-project");
        outboxOptions.CollectionName.ShouldBe("outbox-messages");

        sagaBuilder.ProjectIdValue.ShouldBe("saga-project");
        sagaOptions.CollectionName.ShouldBe("sagas");

        cdcBuilder.ProjectIdValue.ShouldBe("cdc-project");
        cdcOptions.CollectionPath.ShouldBe("cdc-changes");
    }

    [Fact]
    public void MultipleBuilders_MaintainConnectionIsolation()
    {
        // Arrange
        var esOptions = new FirestoreEventStoreOptions();
        var esBuilder = new FirestoreEventSourcingBuilder(esOptions);

        var sagaOptions = new FirestoreSagaOptions();
        var sagaBuilder = new FirestoreSagaBuilder(sagaOptions);

        // Act - configure one with ProjectId, other with EmulatorHost
        esBuilder.ProjectId("es-project");
        sagaBuilder.EmulatorHost("localhost:8080");

        // Assert - each has independent connection state
        esBuilder.ProjectIdValue.ShouldBe("es-project");
        esBuilder.EmulatorHostValue.ShouldBeNull();

        sagaBuilder.EmulatorHostValue.ShouldBe("localhost:8080");
        sagaBuilder.ProjectIdValue.ShouldBeNull();
    }

    [Fact]
    public void MultipleBuilders_SupportOrderIndependentConfiguration()
    {
        // Arrange & Act - configure builders in any order
        var outboxOptions = new FirestoreOutboxOptions();
        var outboxBuilder = new FirestoreOutboxBuilder(outboxOptions);
        outboxBuilder.CollectionName("outbox-first").ProjectId("outbox-project").CredentialsPath("/path/outbox.json");

        var inboxOptions = new FirestoreInboxOptions();
        var inboxBuilder = new FirestoreInboxBuilder(inboxOptions);
        inboxBuilder.CredentialsPath("/path/inbox.json").CollectionName("inbox-first").ProjectId("inbox-project");

        // Assert - both have the expected values regardless of call order
        outboxOptions.CollectionName.ShouldBe("outbox-first");
        outboxBuilder.ProjectIdValue.ShouldBe("outbox-project");
        outboxBuilder.CredentialsPathValue.ShouldBe("/path/outbox.json");

        inboxOptions.CollectionName.ShouldBe("inbox-first");
        inboxBuilder.ProjectIdValue.ShouldBe("inbox-project");
        inboxBuilder.CredentialsPathValue.ShouldBe("/path/inbox.json");
    }
}
