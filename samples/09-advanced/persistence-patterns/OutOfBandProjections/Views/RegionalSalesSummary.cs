// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace OutOfBandProjections.Views;

/// <summary>
/// Materialized view that aggregates sales data by region.
/// Updated out-of-band by the MaterializedViewRefreshService.
/// </summary>
public sealed class RegionalSalesSummary
{
    public string Region { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public int ShippedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CancelledRevenue { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
