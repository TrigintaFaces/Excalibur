// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Excalibur.EventSourcing.Projections;

internal static class ProjectionTenantValidation
{
    internal static ProjectionTenantBoundary Read(IServiceProvider store) =>
        (store.GetService(typeof(IProjectionTenantBoundary)) as IProjectionTenantBoundary)?.Boundary
        ?? throw new InvalidOperationException("The projection store must declare its actual tenant boundary through IProjectionTenantBoundary. An unknown boundary cannot certify projection progress.");

    internal static string? Scope(IServiceProvider store, string? cluster) => Scope(Read(store), cluster);

    internal static string? Scope(ProjectionTenantBoundary boundary, string? cluster)
    {
        if (string.IsNullOrWhiteSpace(cluster))
        {
            if (boundary.TenantId is not null)
            {
                throw new InvalidOperationException("A tenant-confined projection store must provide its actual storage scope. An explicit SubscriptionScope cannot replace the destination's tenant-qualified identity.");
            }
            return null;
        }
        if (boundary.TenantId is { } tenant)
        {
            var framed = tenant.Length.ToString(CultureInfo.InvariantCulture) + ":" + tenant
                + cluster.Length.ToString(CultureInfo.InvariantCulture) + ":" + cluster;
            return "partition/" + Fingerprint(framed);
        }
        return cluster.Length <= 505 ? "estate/" + cluster : "estate-sha256/" + Fingerprint(cluster);
    }

    private static string Fingerprint(string value)
    {
        // Preserve ordinal UTF-16 identity, including unpaired code units. UTF-8 replacement fallback
        // and URI escaping can alias distinct tenant identifiers. Explicit byte order is portable.
        var bytes = new byte[checked(value.Length * 2)];
        for (var index = 0; index < value.Length; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(index * 2, 2), value[index]);
        }
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    internal static void Validate(ProjectionTenantBoundary boundary, string? sourceTenant)
    {
        if (boundary.TenantId is { } tenant && !string.Equals(tenant, sourceTenant, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The committed event's tenant does not match the projection destination's enforced partition. Unknown provenance cannot inherit the ambient tenant. A confined projection cannot consume a mixed-tenant global stream.");
        }
    }
}
