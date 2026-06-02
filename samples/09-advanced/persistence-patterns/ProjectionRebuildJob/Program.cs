// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// ProjectionRebuildJob Sample
// ============================================================================
// This sample demonstrates scheduling a full projection rebuild via Quartz
// using ProjectionRebuildJob from Excalibur.Jobs.
//
// Key concepts:
// - ProjectionRebuildJob        — IBackgroundJob that calls IMaterializedViewProcessor.RebuildAsync
// - IJobConfigurator.AddJob<T>  — Registers a job with a cron schedule
// - IMaterializedViewProcessor  — Rebuilds all registered materialized views
// - IMaterializedViewBuilder<T> — Defines how events map to a view
//
// In production, schedule during low-traffic periods (e.g., "0 0 3 * * ?" = daily at 3 AM).
// This sample uses a short interval for demonstration purposes.
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;
using Excalibur.Jobs.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("==========================================================");
Console.WriteLine("  ProjectionRebuildJob Sample");
Console.WriteLine("  Quartz-Scheduled Full Projection Rebuild");
Console.WriteLine("==========================================================");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information);

// ============================================================================
// Step 1: Register Event Sourcing with In-Memory Provider
// ============================================================================

builder.Services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es
        .UseInMemory())
    .AddJobs(configurator =>
    {
        // Schedule ProjectionRebuildJob to run every 30 seconds (demo only!)
        // In production: "0 0 3 * * ?" (daily at 3 AM)
        configurator.AddJob<ProjectionRebuildJob>("0/30 * * * * ?");
    }));

// ============================================================================
// Step 2: Register Materialized Views
// ============================================================================

builder.Services.AddMaterializedViews(views => views
    .AddBuilder<SalesDashboardView, SalesDashboardViewBuilder>());

// ============================================================================
// Step 3: Seed some events so the rebuild has data to process
// ============================================================================

builder.Services.AddHostedService<SeedEventsService>();

var host = builder.Build();
await host.RunAsync();

// ============================================================================
// Domain Events
// ============================================================================

public sealed record OrderPlaced(string OrderId, string AggregateId, decimal Amount, string Region)
    : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(OrderPlaced);
    public IDictionary<string, object>? Metadata { get; init; }
}

// ============================================================================
// Materialized View
// ============================================================================

public sealed class SalesDashboardView
{
    public string ViewId { get; set; } = "sales-dashboard";
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public Dictionary<string, decimal> RevenueByRegion { get; set; } = new();
    public DateTimeOffset LastUpdated { get; set; }
}

public sealed class SalesDashboardViewBuilder : IMaterializedViewBuilder<SalesDashboardView>
{
    public string ViewName => "SalesDashboard";

    public IReadOnlyList<Type> HandledEventTypes { get; } = [typeof(OrderPlaced)];

    public string? GetViewId(IDomainEvent @event) => @event is OrderPlaced ? "sales-dashboard" : null;

    public SalesDashboardView Apply(SalesDashboardView view, IDomainEvent @event)
    {
        if (@event is OrderPlaced placed)
        {
            view.TotalOrders++;
            view.TotalRevenue += placed.Amount;
            view.RevenueByRegion[placed.Region] =
                view.RevenueByRegion.GetValueOrDefault(placed.Region) + placed.Amount;
            view.LastUpdated = placed.OccurredAt;
        }

        return view;
    }
}

// ============================================================================
// Seed Service — Appends events so rebuild has something to process
// ============================================================================

public sealed class SeedEventsService(
    IEventStore eventStore,
    ILogger<SeedEventsService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding 5 order events...");

        var events = new List<IDomainEvent>
        {
            new OrderPlaced("ord-1", "customer-1", 99.95m, "US-West") { Version = 0 },
            new OrderPlaced("ord-2", "customer-1", 149.99m, "US-East") { Version = 1 },
            new OrderPlaced("ord-3", "customer-2", 29.95m, "EU-West") { Version = 0 },
            new OrderPlaced("ord-4", "customer-2", 199.99m, "US-West") { Version = 1 },
            new OrderPlaced("ord-5", "customer-3", 59.99m, "EU-West") { Version = 0 },
        };

        await eventStore.AppendAsync("customer-1", "Customer", events.Take(2).ToList(), -1, cancellationToken);
        await eventStore.AppendAsync("customer-2", "Customer", events.Skip(2).Take(2).ToList(), -1, cancellationToken);
        await eventStore.AppendAsync("customer-3", "Customer", events.Skip(4).Take(1).ToList(), -1, cancellationToken);

        logger.LogInformation("Seeding complete. ProjectionRebuildJob will process on next scheduled run.");
        logger.LogInformation("Watch for '[ProjectionRebuildJob] Starting...' and '[ProjectionRebuildJob] Completed' log messages.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
