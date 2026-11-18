// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Names the public timer cancellation domain, independent of saga state type.</summary>
public sealed class SagaTimerRecoveryDomain
{
    /// <summary>Creates exact tenant and saga receiver identity.</summary>
    /// <param name="tenantId">Exact concrete tenant partition; never trimmed or case-folded.</param>
    /// <param name="sagaId">The coordinator's nonempty saga identifier.</param>
    public SagaTimerRecoveryDomain(string tenantId, Guid sagaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tenantId.Length, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
        if (sagaId == Guid.Empty) { throw new ArgumentException("A tracked saga identity must be nonempty.", nameof(sagaId)); }
        TenantId = tenantId; SagaId = sagaId;
    }
    /// <summary>Gets the exact authoritative tenant.</summary>
    public string TenantId { get; }
    /// <summary>Gets the saga identity shared by all state-type views and public cancellations.</summary>
    public Guid SagaId { get; }
}
