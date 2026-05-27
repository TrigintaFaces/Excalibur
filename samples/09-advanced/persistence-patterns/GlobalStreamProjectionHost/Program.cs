// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// GlobalStreamProjectionHost Sample
// ============================================================================
// This sample demonstrates continuous global stream tailing using
// GlobalStreamProjectionHost<TState> — a BackgroundService that reads
// ALL events from the global event stream and applies them to a custom
// projection.
//
// Key concepts:
// - GlobalStreamProjectionHost<TState> — Background service that polls the global stream
// - IGlobalStreamProjection<TState>    — Your custom projection logic (apply events to state)
// - GlobalStreamProjectionOptions      — Configuration (batch size, poll interval, checkpoints)
// - ISubscriptionCheckpointStore       — Tracks read position across restarts
//
// Comparison with other projection modes:
// - AddProjection<T>().Inline()           → Runs during SaveAsync (single aggregate)
// - AddProjection<T>().Async()            → Background catch-up via AsyncProjectionProcessingHost
// - GlobalStreamProjectionHost<TState>    → Custom logic over ALL events (this sample)
//
// Use GlobalStreamProjectionHost when you need full control over cross-aggregate
// state that doesn't fit the standard projection model.
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

Console.WriteLine("==========================================================");
Console.WriteLine("  GlobalStreamProjectionHost Sample");
Console.WriteLine("  Continuous Global Stream Tailing");
Console.WriteLine("==========================================================");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information);

// ============================================================================
// Step 1: Register Event Sourcing Infrastructure
// ============================================================================

builder.Services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .UseInMemory()));

// ============================================================================
// Step 2: Register the Custom Global Stream Projection
// ============================================================================

// The projection implements IGlobalStreamProjection<TState>
builder.Services.AddSingleton<IGlobalStreamProjection<SystemMetricsState>, SystemMetricsProjection>();

// Configure the host options — each host MUST have a unique ProjectionName
builder.Services.Configure<GlobalStreamProjectionOptions>(opts =>
{
    opts.ProjectionName = "SystemMetrics";    // Unique name for checkpoint tracking
    opts.BatchSize = 100;                     // Events per poll batch
    opts.IdlePollingInterval = TimeSpan.FromSeconds(2);  // Wait between empty polls
    opts.CheckpointInterval = 50;             // Save checkpoint every N events
});

// Register the host as a BackgroundService
builder.Services.AddHostedService<GlobalStreamProjectionHost<SystemMetricsState>>();

// ============================================================================
// Step 3: Seed events continuously so the host has data to process
// ============================================================================

builder.Services.AddHostedService<EventGeneratorService>();

var host = builder.Build();
await host.RunAsync();

// ============================================================================
// Custom State — Tracks cross-aggregate metrics
// ============================================================================

public sealed class SystemMetricsState
{
    public long TotalEventsProcessed { get; set; }
    public long TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public long TotalPayments { get; set; }
    public DateTimeOffset LastEventTimestamp { get; set; }
}

// ============================================================================
// Custom Projection — IGlobalStreamProjection<TState>
// ============================================================================

public sealed class SystemMetricsProjection(
    ILogger<SystemMetricsProjection> logger) : IGlobalStreamProjection<SystemMetricsState>
{
    public Task ApplyAsync(
        IDomainEvent domainEvent,
        SystemMetricsState state,
        CancellationToken cancellationToken)
    {
        state.TotalEventsProcessed++;
        state.LastEventTimestamp = domainEvent.OccurredAt;

        switch (domainEvent)
        {
            case OrderPlaced e:
                state.TotalOrders++;
                state.TotalRevenue += e.Amount;
                logger.LogInformation(
                    "Order #{OrderCount}: ${Amount} — Total revenue: ${Revenue:F2}",
                    state.TotalOrders, e.Amount, state.TotalRevenue);
                break;

            case PaymentReceived:
                state.TotalPayments++;
                break;
        }

        return Task.CompletedTask;
    }
}

// ============================================================================
// Domain Events
// ============================================================================

public sealed record OrderPlaced(string OrderId, string AggregateId, decimal Amount)
    : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(OrderPlaced);
    public IDictionary<string, object>? Metadata { get; init; }
}

public sealed record PaymentReceived(string PaymentId, string AggregateId, decimal Amount)
    : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(PaymentReceived);
    public IDictionary<string, object>? Metadata { get; init; }
}

// ============================================================================
// Event Generator — Produces events for the host to consume
// ============================================================================

public sealed class EventGeneratorService(
    IEventStore eventStore,
    ILogger<EventGeneratorService> logger) : BackgroundService
{
    private int _orderCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait briefly for host to start
        await Task.Delay(1000, stoppingToken);

        logger.LogInformation("Starting event generation (1 event every 3 seconds)...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _orderCount++;
            var customerId = $"customer-{_orderCount % 5 + 1}";
            var amount = Random.Shared.Next(10, 500) + 0.99m;

            var events = new List<IDomainEvent>
            {
                new OrderPlaced($"ord-{_orderCount}", customerId, amount) { Version = _orderCount - 1 },
                new PaymentReceived($"pay-{_orderCount}", customerId, amount) { Version = _orderCount },
            };

            await eventStore.AppendAsync(
                customerId, "Customer", events, _orderCount == 1 ? -1 : (_orderCount - 1) * 2 - 1,
                stoppingToken);

            await Task.Delay(3000, stoppingToken);
        }
    }
}
