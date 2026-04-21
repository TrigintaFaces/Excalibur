// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace ElasticSearch_Projections.ReadModels;

/// <summary>
/// A read model (projection) representing a customer dashboard for the CQRS read side.
/// </summary>
public class CustomerDashboard
{
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal LifetimeSpend { get; set; }
    public DateTimeOffset LastOrderDate { get; set; }
}
