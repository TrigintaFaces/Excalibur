// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Names the authoritative storage scope before an incarnation or speculative transition is allocated.</summary>
public sealed class SagaScopeIdentity
{
    /// <summary>Initializes an exact tenant and saga storage scope.</summary>
    /// <param name="tenantId">The exact authoritative tenant partition.</param>
    /// <param name="stateType">The registered persisted state type name, matching existing state-store identity.</param>
    /// <param name="sagaId">The saga identifier.</param>
    public SagaScopeIdentity(string tenantId,string stateType,Guid sagaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateType);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tenantId.Length,Excalibur.Dispatch.TenantId.MaxLength,nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stateType.Length,500,nameof(stateType));
        if(sagaId==Guid.Empty){throw new ArgumentException("Saga identifier must be nonempty.",nameof(sagaId));}
        TenantId=tenantId;StateType=stateType;SagaId=sagaId;
    }
    /// <summary>Gets the exact tenant authority.</summary>
    public string TenantId { get; }
    /// <summary>Gets the persisted state type name, not the timeout's saga-routing type.</summary>
    public string StateType { get; }
    /// <summary>Gets the saga identifier.</summary>
    public Guid SagaId { get; }
}
