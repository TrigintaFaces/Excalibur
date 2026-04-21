// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionBuilderKeyedByShould
{
    private readonly InMemoryProjectionRegistry _registry = new();

    [Fact]
    public async Task DeriveProjectionIdFromEventForSyncHandler()
    {
        // Arrange
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"region-{e.Amount}");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total = e.Amount;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 100m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- stored under derived key, NOT aggregate ID
        store.Get("region-100").ShouldNotBeNull();
        store.Get("region-100")!.Total.ShouldBe(100m);
        store.Get("agg-1").ShouldBeNull();
    }

    [Fact]
    public async Task FallBackToAggregateIdWhenNoKeySelectorRegistered()
    {
        // Arrange
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"custom-{e.Amount}");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total = e.Amount;
            proj.EventCount++;
        });
        builder.When<TestOrderShipped>((proj, e) =>
        {
            proj.ShippedAt = e.ShippedAt;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var shippedAt = DateTimeOffset.UtcNow;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "order-1", Amount = 50m, Version = 1 },
            new TestOrderShipped { AggregateId = "order-1", ShippedAt = shippedAt, Version = 2 }
        };
        var context = new EventNotificationContext("order-1", "Order", 2, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- OrderPlaced stored under derived key
        var keyedProjection = store.Get("custom-50");
        keyedProjection.ShouldNotBeNull();
        keyedProjection.Total.ShouldBe(50m);
        keyedProjection.EventCount.ShouldBe(1);

        // Assert -- OrderShipped stored under aggregate ID (no KeyedBy registered)
        var defaultProjection = store.Get("order-1");
        defaultProjection.ShouldNotBeNull();
        defaultProjection.ShippedAt.ShouldBe(shippedAt);
        defaultProjection.EventCount.ShouldBe(1);
    }

    [Fact]
    public async Task HandleMultipleKeysInSameBatch()
    {
        // Arrange
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"key-{e.Amount}");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total += e.Amount;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 100m, Version = 1 },
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 200m, Version = 2 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 2, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- two distinct projections created
        var proj100 = store.Get("key-100");
        proj100.ShouldNotBeNull();
        proj100.Total.ShouldBe(100m);
        proj100.EventCount.ShouldBe(1);

        var proj200 = store.Get("key-200");
        proj200.ShouldNotBeNull();
        proj200.Total.ShouldBe(200m);
        proj200.EventCount.ShouldBe(1);

        // Assert -- nothing stored under the aggregate ID
        store.Get("agg-1").ShouldBeNull();
    }

    [Fact]
    public async Task CreateNewProjectionWhenKeyedProjectionDoesNotExist()
    {
        // Arrange -- store is empty
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"tenant-{e.Amount}");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total = e.Amount;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 75m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- new projection created under derived key
        var created = store.Get("tenant-75");
        created.ShouldNotBeNull();
        created.Total.ShouldBe(75m);
        created.EventCount.ShouldBe(1);
    }

    [Fact]
    public async Task MergeWithExistingKeyedProjectionState()
    {
        // Arrange -- pre-populate store with existing projection
        var store = new InMemoryProjectionStore<OrderSummary>();
        await store.UpsertAsync("category-electronics",
            new OrderSummary { Total = 500m, EventCount = 3 },
            CancellationToken.None).ConfigureAwait(false);

        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(_ => "category-electronics");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total += e.Amount;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 100m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- existing state merged, not replaced
        var merged = store.Get("category-electronics");
        merged.ShouldNotBeNull();
        merged.Total.ShouldBe(600m); // 500 + 100
        merged.EventCount.ShouldBe(4); // 3 + 1
    }

    [Fact]
    public void RejectNullKeySelector()
    {
        // Arrange
        var builder = new ProjectionBuilder<OrderSummary>(_registry);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.KeyedBy<TestOrderPlaced>(null!));
    }

    [Fact]
    public void SupportFluentChainingWithKeyedBy()
    {
        // Arrange
        var builder = new ProjectionBuilder<OrderSummary>(_registry);

        // Act -- chain Inline, KeyedBy, When, KeyedBy, When
        var result = builder
            .Inline()
            .KeyedBy<TestOrderPlaced>(e => $"key-{e.Amount}")
            .When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount)
            .KeyedBy<TestOrderShipped>(e => $"shipped-{e.ShippedAt.Ticks}")
            .When<TestOrderShipped>((proj, e) => proj.ShippedAt = e.ShippedAt);

        // Assert -- all calls return the same builder instance
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public async Task DeriveProjectionIdFromEventForAsyncHandler()
    {
        // Arrange -- KeyedBy with WhenHandledBy (async DI-resolved handler) exercises
        // the full async code path in CreateInlineApplyDelegate that uses GetKeySelector.
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .AddTransient<OrderPlacedHandler>()
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"async-key-{e.Amount}");
        builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 250m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- stored under derived key, NOT aggregate ID
        store.Get("async-key-250").ShouldNotBeNull();
        store.Get("async-key-250")!.Total.ShouldBe(250m);
        store.Get("agg-1").ShouldBeNull();
    }

    [Fact]
    public async Task FallBackToAggregateIdForAsyncHandlerWithoutKeySelector()
    {
        // Arrange -- async handler WITHOUT KeyedBy should use aggregate ID
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .AddTransient<OrderPlacedHandler>()
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.WhenHandledBy<TestOrderPlaced, OrderPlacedHandler>();
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "order-42", Amount = 300m, Version = 1 }
        };
        var context = new EventNotificationContext("order-42", "Order", 1, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- stored under aggregate ID (no KeyedBy registered)
        store.Get("order-42").ShouldNotBeNull();
        store.Get("order-42")!.Total.ShouldBe(300m);
    }

    [Fact]
    public async Task ThrowWhenKeySelectorReturnsNull()
    {
        // Arrange -- key selector returns null at runtime
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(_ => null!);
        builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 100m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => registration.InlineApply!(events, context, services, CancellationToken.None));
        ex.Message.ShouldContain("null or empty projection ID");
    }

    [Fact]
    public async Task ThrowWhenKeySelectorReturnsEmptyString()
    {
        // Arrange -- key selector returns empty string at runtime
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(_ => string.Empty);
        builder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 50m, Version = 1 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => registration.InlineApply!(events, context, services, CancellationToken.None));
        ex.Message.ShouldContain("null or empty projection ID");
    }

    [Fact]
    public async Task SkipUnhandledEventsInSyncKeyedPath()
    {
        // Arrange -- register KeyedBy for TestOrderPlaced but NO handler for TestOrderShipped.
        // TestOrderShipped events should be skipped (no store load, no upsert).
        var store = new InMemoryProjectionStore<OrderSummary>();
        var services = new ServiceCollection()
            .AddSingleton<IProjectionStore<OrderSummary>>(store)
            .BuildServiceProvider();

        var builder = new ProjectionBuilder<OrderSummary>(_registry);
        builder.Inline();
        builder.KeyedBy<TestOrderPlaced>(e => $"key-{e.Amount}");
        builder.When<TestOrderPlaced>((proj, e) =>
        {
            proj.Total = e.Amount;
            proj.EventCount++;
        });
        builder.Build();

        var registration = _registry.GetRegistration(typeof(OrderSummary))!;
        var events = new List<Dispatch.Abstractions.IDomainEvent>
        {
            new TestOrderPlaced { AggregateId = "agg-1", Amount = 100m, Version = 1 },
            new TestOrderShipped { AggregateId = "agg-1", ShippedAt = DateTimeOffset.UtcNow, Version = 2 }
        };
        var context = new EventNotificationContext("agg-1", "Order", 2, DateTimeOffset.UtcNow);

        // Act
        await registration.InlineApply!(events, context, services, CancellationToken.None)
            .ConfigureAwait(false);

        // Assert -- only the handled event produced a projection; unhandled event was skipped
        store.Get("key-100").ShouldNotBeNull();
        store.Get("key-100")!.EventCount.ShouldBe(1);

        // No projection under aggregate ID (the unhandled event was skipped, not stored)
        store.Get("agg-1").ShouldBeNull();
    }
}
