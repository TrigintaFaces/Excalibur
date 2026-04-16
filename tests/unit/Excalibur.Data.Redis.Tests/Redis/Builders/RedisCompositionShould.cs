// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;
using Excalibur.EventSourcing.Redis;
using Excalibur.Inbox.Redis;
using Excalibur.LeaderElection.Redis;
using Excalibur.Outbox.Redis;

using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis.Builders;

/// <summary>
/// Multi-subsystem Redis composition test — verifies that all Redis
/// builder-enabled subsystems can be composed together without conflicts.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class RedisCompositionShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";

    [Fact]
    public void ComposeMultipleRedisBuilders_WithSharedMultiplexer()
    {
        var multiplexer = A.Fake<IConnectionMultiplexer>();

        var esBuilder = new RedisEventSourcingBuilder(new RedisEventStoreOptions());
        esBuilder.ConnectionMultiplexer(multiplexer);

        var inboxBuilder = new RedisInboxBuilder(new RedisInboxOptions());
        inboxBuilder.ConnectionMultiplexer(multiplexer);

        var outboxBuilder = new RedisOutboxBuilder(new RedisOutboxOptions());
        outboxBuilder.ConnectionMultiplexer(multiplexer);

        var dataBuilder = new RedisDataBuilder(new RedisProviderOptions());
        dataBuilder.ConnectionMultiplexer(multiplexer);

        var leBuilder = new RedisLeaderElectionBuilder();
        leBuilder.ConnectionMultiplexer(multiplexer);

        // All share the same multiplexer independently
        esBuilder.MultiplexerInstance.ShouldBe(multiplexer);
        inboxBuilder.MultiplexerInstance.ShouldBe(multiplexer);
        outboxBuilder.MultiplexerInstance.ShouldBe(multiplexer);
        dataBuilder.MultiplexerInstance.ShouldBe(multiplexer);
        leBuilder.MultiplexerInstance.ShouldBe(multiplexer);
    }

    [Fact]
    public void ComposeWithIndependentOptions()
    {
        var inboxOptions = new RedisInboxOptions();
        var inboxBuilder = new RedisInboxBuilder(inboxOptions);
        inboxBuilder.ConnectionString(TestConnectionString).KeyPrefix("inbox:");

        var outboxOptions = new RedisOutboxOptions();
        var outboxBuilder = new RedisOutboxBuilder(outboxOptions);
        outboxBuilder.ConnectionString(TestConnectionString).KeyPrefix("outbox:");

        inboxOptions.KeyPrefix.ShouldBe("inbox:");
        outboxOptions.KeyPrefix.ShouldBe("outbox:");
    }

    [Fact]
    public void ComposeInAnyOrder()
    {
        // Order 1
        var es1 = new RedisEventSourcingBuilder(new RedisEventStoreOptions());
        var inbox1 = new RedisInboxBuilder(new RedisInboxOptions());
        es1.ConnectionString(TestConnectionString);
        inbox1.ConnectionString(TestConnectionString);

        // Order 2 (reversed)
        var inbox2 = new RedisInboxBuilder(new RedisInboxOptions());
        var es2 = new RedisEventSourcingBuilder(new RedisEventStoreOptions());
        inbox2.ConnectionString(TestConnectionString);
        es2.ConnectionString(TestConnectionString);

        es1.ShouldNotBeNull();
        es2.ShouldNotBeNull();
    }
}
