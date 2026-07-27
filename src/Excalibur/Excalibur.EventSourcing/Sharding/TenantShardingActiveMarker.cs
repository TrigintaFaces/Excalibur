// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Marker registered when tenant-sharding routing is active. Both sharding entry points route through
/// <c>RegisterTenantRoutingStores</c> (the <c>EnableTenantSharding</c> builder seam and <c>AddMultiTenancy</c>'s
/// sharding strategy), so its presence means sharding is enabled regardless of how it was configured.
/// </summary>
/// <remarks>
/// Consumed by the event-store erasure startup gate to fail closed on the (not-yet-supported) sharding +
/// erasure composition, rather than resolve an erasure contributor that cannot route erasure to per-tenant
/// shards. A non-sharding host never registers this marker.
/// </remarks>
internal sealed class TenantShardingActiveMarker;
