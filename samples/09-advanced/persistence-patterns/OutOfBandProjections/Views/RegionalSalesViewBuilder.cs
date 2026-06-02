// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using OutOfBandProjections.Domain;

namespace OutOfBandProjections.Views;

/// <summary>
/// Materialized view builder for <see cref="RegionalSalesSummary"/>.
/// Implements <see cref="IMaterializedViewBuilder{TView}"/> to define how events
/// map to the view. The <see cref="GetViewId"/> method returns the region,
/// aggregating all orders from the same region into one summary view.
/// </summary>
public sealed class RegionalSalesViewBuilder : IMaterializedViewBuilder<RegionalSalesSummary>
{
    /// <inheritdoc />
    public string ViewName => "RegionalSalesSummary";

    /// <inheritdoc />
    public IReadOnlyList<Type> HandledEventTypes { get; } =
    [
        typeof(OrderPlaced),
        typeof(OrderShipped),
        typeof(OrderCancelled),
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Returns the region from the event, so all orders in the same region
    /// share one <see cref="RegionalSalesSummary"/> instance.
    /// </remarks>
    public string? GetViewId(IDomainEvent @event) => @event switch
    {
        OrderPlaced e => e.Region,
        // OrderShipped and OrderCancelled don't carry Region directly.
        // In production, you'd look up the region from a read model or
        // store it on the aggregate. For this sample, we use the AggregateId
        // as a fallback and demonstrate the processor handles null gracefully.
        OrderShipped => null, // Skipped — region unknown without lookup
        OrderCancelled => null, // Skipped — region unknown without lookup
        _ => null,
    };

    /// <inheritdoc />
    public RegionalSalesSummary Apply(RegionalSalesSummary view, IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced e:
                view.Region = e.Region;
                view.TotalOrders++;
                view.TotalRevenue += e.TotalAmount;
                view.LastUpdated = e.OccurredAt;
                break;

            case OrderShipped:
                view.ShippedOrders++;
                view.LastUpdated = @event.OccurredAt;
                break;

            case OrderCancelled e:
                view.CancelledOrders++;
                view.CancelledRevenue += 0; // Would need order amount lookup
                view.LastUpdated = e.OccurredAt;
                break;
        }

        return view;
    }
}
