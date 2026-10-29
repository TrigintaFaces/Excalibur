// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>Describes the tenant boundary enforced by the actual projection destination.</summary>
/// <remarks>Resolve through the outer store's service-provider interface. Absence means unknown,
/// not estate-wide. Read one immutable snapshot in the operation's execution scope. A decorator
/// must preserve the actual boundary and must not infer confinement merely from a tenant being present.</remarks>
public interface IProjectionTenantBoundary
{
    /// <summary>Gets the boundary of the destination used by this operation.</summary>
    ProjectionTenantBoundary Boundary { get; }
}

/// <summary>An immutable declaration of estate-wide storage or one enforced tenant partition.</summary>
public sealed record ProjectionTenantBoundary : IProjectionTenantBoundary
{
    private ProjectionTenantBoundary(string? tenantId) => TenantId = tenantId;

    /// <summary>Gets the explicit estate-wide boundary, which provides no tenant isolation.</summary>
    public static ProjectionTenantBoundary EstateWide { get; } = new((string?)null);

    /// <summary>Gets the enforced tenant, or null for explicitly estate-wide storage.</summary>
    public string? TenantId { get; }

    /// <summary>Declares a partition actually enforced by the underlying store.</summary>
    /// <param name="tenantId">The nonblank tenant identifier, including the explicit untenanted sentinel.</param>
    /// <returns>An immutable partition boundary.</returns>
    public static ProjectionTenantBoundary Partition(string tenantId) => new(
        string.Equals(tenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal)
            ? TenantScope.UntenantedSentinel : TenantScope.Scoped(tenantId).TenantId);

    ProjectionTenantBoundary IProjectionTenantBoundary.Boundary => this;
}
