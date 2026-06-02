// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Compliance.Stores.MongoDb;
using Excalibur.Data.MongoDB.Authorization;
using Excalibur.Data.MongoDB.MaterializedViews;
using Excalibur.Data.MongoDB.Projections;
using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.Inbox.MongoDB;
using Excalibur.Outbox.MongoDB;
using Excalibur.Saga.MongoDB;

using Excalibur.Data.Tests.MongoDB.Projections;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB;

/// <summary>
/// Verifies the <c>_ownsClient</c> disposal pattern introduced by the MongoDB.Driver 3.x migration.
/// In v3, <see cref="MongoClient"/> implements <see cref="IDisposable"/>.
/// Stores that create their own client MUST dispose it; stores that receive an injected client MUST NOT.
/// </summary>
/// <remarks>
/// Sprint 816: MongoDB.Driver 2.x → 3.x migration verification.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Disposal")]
public sealed class MongoDbClientOwnershipDisposalShould : UnitTestBase
{
    /// <summary>
    /// Creates a faked IMongoClient + IMongoDatabase pair.
    /// We do NOT fake IMongoCollection because internal document types
    /// prevent FakeItEasy from creating proxies.
    /// The stores store a null collection reference but never use it during disposal.
    /// </summary>
    private static (IMongoClient Client, IMongoDatabase Database) CreateFakeClientAndDatabase()
    {
        var client = A.Fake<IMongoClient>();
        var database = A.Fake<IMongoDatabase>();
        A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings?>._)).Returns(database);
        return (client, database);
    }

    // ========== Inbox Store ==========

    [Fact]
    public async Task InboxStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbInboxOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "inbox"
        });

        var store = new MongoDbInboxStore(client, options, A.Fake<ILogger<MongoDbInboxStore>>());

        // Act
        await store.DisposeAsync();

        // Assert — injected client must NOT be disposed
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public async Task InboxStore_DoubleDisposeAsync_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new MongoDbInboxOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "inbox"
        });

        var store = new MongoDbInboxStore(options, A.Fake<ILogger<MongoDbInboxStore>>());

        // Act & Assert — idempotent disposal
        await store.DisposeAsync();
        await store.DisposeAsync();
    }

    // ========== Outbox Store ==========

    [Fact]
    public async Task OutboxStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbOutboxOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "outbox"
        });

        var store = new MongoDbOutboxStore(client, options, A.Fake<ILogger<MongoDbOutboxStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Saga Store ==========

    [Fact]
    public async Task SagaStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbSagaOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "sagas"
        });

        var store = new MongoDbSagaStore(client, options, A.Fake<ILogger<MongoDbSagaStore>>(),
            new DispatchJsonSerializer());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Snapshot Store ==========

    [Fact]
    public async Task SnapshotStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbSnapshotStoreOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "snapshots"
        });

        var store = new MongoDbSnapshotStore(client, options, A.Fake<ILogger<MongoDbSnapshotStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Event Store ==========

    [Fact]
    public async Task EventStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbEventStoreOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "events"
        });

        var store = new MongoDbEventStore(client, options, A.Fake<ILogger<MongoDbEventStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Projection Store (generic — uses TestProjection) ==========

    [Fact]
    public async Task ProjectionStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbProjectionStoreOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test",
            CollectionName = "projections"
        });

        var store = new MongoDbProjectionStore<TestProjection>(client, options,
            A.Fake<ILogger<MongoDbProjectionStore<TestProjection>>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Materialized View Store ==========

    [Fact]
    public async Task MaterializedViewStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbMaterializedViewStoreOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test"
        });

        var store = new MongoDbMaterializedViewStore(client, options,
            A.Fake<ILogger<MongoDbMaterializedViewStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Grant Store ==========

    [Fact]
    public async Task GrantStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbAuthorizationOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test"
        });

        var store = new MongoDbGrantStore(client, options, A.Fake<ILogger<MongoDbGrantStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Activity Group Grant Store ==========

    [Fact]
    public async Task ActivityGroupGrantStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbAuthorizationOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test"
        });

        var store = new MongoDbActivityGroupGrantStore(client, options,
            A.Fake<ILogger<MongoDbActivityGroupGrantStore>>());

        // Act
        await store.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== CDC Processor (always injected, never owns) ==========

    [Fact]
    public void CdcProcessor_WithInjectedClient_NotDisposeClientOnDispose()
    {
        // Arrange
        var client = A.Fake<IMongoClient>();
        var options = Options.Create(new MongoDbCdcOptions
        {
            ProcessorId = "test",
            Connection = { ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0" }
        });

        var processor = new MongoDbCdcProcessor(client, options,
            A.Fake<IMongoDbCdcStateStore>(), A.Fake<ILogger<MongoDbCdcProcessor>>());

        // Act
        processor.Dispose();

        // Assert — CDC processor always receives injected client, must never dispose it
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public async Task CdcProcessor_WithInjectedClient_NotDisposeClientOnDisposeAsync()
    {
        // Arrange
        var client = A.Fake<IMongoClient>();
        var options = Options.Create(new MongoDbCdcOptions
        {
            ProcessorId = "test",
            Connection = { ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0" }
        });

        var processor = new MongoDbCdcProcessor(client, options,
            A.Fake<IMongoDbCdcStateStore>(), A.Fake<ILogger<MongoDbCdcProcessor>>());

        // Act
        await processor.DisposeAsync();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    // ========== Compliance Store ==========

    [Fact]
    public void ComplianceStore_WithInjectedClient_NotDisposeClient()
    {
        // Arrange
        var (client, _) = CreateFakeClientAndDatabase();
        var options = Options.Create(new MongoDbComplianceOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test"
        });

        var store = new MongoDbComplianceStore(client, options, A.Fake<ILogger<MongoDbComplianceStore>>());

        // Act
        store.Dispose();

        // Assert
        A.CallTo(() => ((IDisposable)client).Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public void ComplianceStore_DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new MongoDbComplianceOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "test"
        });

        var store = new MongoDbComplianceStore(options, A.Fake<ILogger<MongoDbComplianceStore>>());

        // Act & Assert — idempotent disposal
        store.Dispose();
        store.Dispose();
    }
}
