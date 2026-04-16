// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.Postgres;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Outbox.Postgres;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Postgres;
using Excalibur.Saga.Postgres.DependencyInjection;

namespace Excalibur.Data.Tests.Postgres.Builders;

/// <summary>
/// Multi-subsystem Postgres composition test (8x9pkt) — verifies that all Postgres
/// builder-enabled subsystems can be composed together without DI conflicts.
/// </summary>
/// <remarks>
/// This test validates the Postgres API Unification epic outcome: every subsystem uses
/// the canonical 5-overload builder pattern and their DI registrations coexist cleanly.
/// Tests verify options isolation — each subsystem's options are independent.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresCompositionShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    [Fact]
    public void ComposeMultiplePostgresBuilders_WithoutDiConflicts()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — register Saga, Inbox, and LeaderElection with Postgres builders
        services.AddExcaliburSaga((Action<ISagaBuilder>)(saga =>
            saga.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("dispatch"))));

        services.AddExcaliburInbox(inbox =>
            inbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("messaging")));

        services.AddExcaliburOutbox(outbox =>
            outbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString).SchemaName("outbox")));

        // Assert — all registrations coexist
        var provider = services.BuildServiceProvider();
        provider.ShouldNotBeNull();
        services.ShouldNotBeEmpty();
    }

    [Fact]
    public void RegisterMultiplePostgresSubsystems_WithIndependentOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — each subsystem gets different schema/table configuration
        services.AddExcaliburSaga((Action<ISagaBuilder>)(saga =>
            saga.UsePostgres(pg => pg
                .ConnectionString(TestConnectionString)
                .SchemaName("saga_schema")
                .TableName("my_sagas"))));

        services.AddExcaliburInbox(inbox =>
            inbox.UsePostgres(pg => pg
                .ConnectionString(TestConnectionString)
                .SchemaName("inbox_schema")
                .TableName("my_inbox")
                .MaxRetryCount(5)));

        // Assert — options are independently configured
        var provider = services.BuildServiceProvider();

        var sagaOptions = provider.GetRequiredService<IOptions<PostgresSagaOptions>>();
        sagaOptions.Value.Schema.ShouldBe("saga_schema");
        sagaOptions.Value.TableName.ShouldBe("my_sagas");

        var inboxOptions = provider.GetRequiredService<IOptions<PostgresInboxOptions>>();
        inboxOptions.Value.SchemaName.ShouldBe("inbox_schema");
        inboxOptions.Value.TableName.ShouldBe("my_inbox");
        inboxOptions.Value.MaxRetryCount.ShouldBe(5);
    }

    [Fact]
    public void RegisterSubsystems_InAnyOrder()
    {
        // Arrange & Act — order 1
        var services1 = new ServiceCollection();
        services1.AddExcaliburInbox(inbox =>
            inbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
        services1.AddExcaliburSaga((Action<ISagaBuilder>)(saga =>
            saga.UsePostgres(pg => pg.ConnectionString(TestConnectionString))));
        var provider1 = services1.BuildServiceProvider();

        // Arrange & Act — order 2 (reversed)
        var services2 = new ServiceCollection();
        services2.AddExcaliburSaga((Action<ISagaBuilder>)(saga =>
            saga.UsePostgres(pg => pg.ConnectionString(TestConnectionString))));
        services2.AddExcaliburInbox(inbox =>
            inbox.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
        var provider2 = services2.BuildServiceProvider();

        // Assert — both succeed
        provider1.ShouldNotBeNull();
        provider2.ShouldNotBeNull();
    }

    [Fact]
    public void RegisterLeaderElection_WithCustomLockKey()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcalibur(excalibur =>
            excalibur.AddLeaderElection(le =>
                le.UsePostgres(pg => pg
                    .ConnectionString(TestConnectionString)
                    .LockKey(42))));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
        options.Value.LockKey.ShouldBe(42);
    }

    [Fact]
    public void RegisterOutbox_WithBuilderFeatures()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburOutbox(outbox =>
            outbox.UsePostgres(pg => pg
                .ConnectionString(TestConnectionString)
                .SchemaName("messaging")
                .TableName("outbox_messages")
                .DeadLetterTableName("outbox_dlq")));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
        options.Value.SchemaName.ShouldBe("messaging");
        options.Value.OutboxTableName.ShouldBe("outbox_messages");
        options.Value.DeadLetterTableName.ShouldBe("outbox_dlq");
    }
}
