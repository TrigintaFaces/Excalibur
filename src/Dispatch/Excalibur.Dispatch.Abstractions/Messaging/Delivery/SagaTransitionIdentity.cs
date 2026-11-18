// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Messaging;

/// <summary>Names one durable transition independently of its expected state version.</summary>
public sealed class SagaTransitionIdentity
{
    /// <summary>Initializes an immutable transition identity without normalizing opaque tenant or delivery identifiers.</summary>
    /// <param name="tenantId">The authoritative nonblank tenant partition, including the reserved untenanted value.</param>
    /// <param name="sagaType">The registered persisted state type name, matching the selected state store's type identity.</param>
    /// <param name="sagaId">The saga identifier.</param>
    /// <param name="incarnationId">The durable incarnation identifier.</param>
    /// <param name="transitionId">The stable transition identifier reused on uncertain outcomes.</param>
    /// <param name="deliveryId">The exact delivery identity, or null when absent.</param>
    public SagaTransitionIdentity(string tenantId, string sagaType, Guid sagaId, Guid incarnationId, Guid transitionId, string? deliveryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
        if (deliveryId is not null) { ArgumentException.ThrowIfNullOrEmpty(deliveryId); }
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tenantId.Length, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sagaType.Length, 500, nameof(sagaType));
        if (sagaId == Guid.Empty || incarnationId == Guid.Empty || transitionId == Guid.Empty)
        {
            throw new ArgumentException("Saga, incarnation and transition identifiers must be nonempty.");
        }

        TenantId = tenantId;
        SagaType = sagaType;
        SagaId = sagaId;
        IncarnationId = incarnationId;
        TransitionId = transitionId;
        DeliveryId = deliveryId;
    }

    /// <summary>Gets the exact authoritative tenant partition.</summary>
    public string TenantId { get; }
    /// <summary>Gets the registered persisted state type name.</summary>
    public string SagaType { get; }
    /// <summary>Gets the saga identifier.</summary>
    public Guid SagaId { get; }
    /// <summary>Gets the durable incarnation identifier.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Gets the transition identifier, whose uniqueness does not include expected version.</summary>
    public Guid TransitionId { get; }
    /// <summary>Gets the original delivery identifier, or null.</summary>
    public string? DeliveryId { get; }
}
