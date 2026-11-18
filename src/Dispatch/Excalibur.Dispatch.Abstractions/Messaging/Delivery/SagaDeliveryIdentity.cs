// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Names an original delivery before a handler or effect preparation runs.</summary>
public sealed class SagaDeliveryIdentity
{
    /// <summary>Initializes an exact delivery authority without allocating a transition identifier.</summary>
    /// <param name="tenantId">The authoritative tenant partition.</param>
    /// <param name="sagaType">The registered stable saga type name.</param>
    /// <param name="sagaId">The saga identifier.</param>
    /// <param name="incarnationId">The durable incarnation.</param>
    /// <param name="deliveryId">The exact nonempty envelope delivery identifier.</param>
    public SagaDeliveryIdentity(string tenantId, string sagaType, Guid sagaId, Guid incarnationId, string deliveryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
        ArgumentException.ThrowIfNullOrEmpty(deliveryId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tenantId.Length, Excalibur.Dispatch.TenantId.MaxLength, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sagaType.Length, 500, nameof(sagaType));
        if (sagaId == Guid.Empty || incarnationId == Guid.Empty) { throw new ArgumentException("Saga and incarnation identifiers must be nonempty."); }
        TenantId = tenantId;
        SagaType = sagaType;
        SagaId = sagaId;
        IncarnationId = incarnationId;
        DeliveryId = deliveryId;
    }
    /// <summary>Gets the exact tenant authority.</summary>
    public string TenantId { get; }
    /// <summary>Gets the stable saga type name.</summary>
    public string SagaType { get; }
    /// <summary>Gets the saga identifier.</summary>
    public Guid SagaId { get; }
    /// <summary>Gets the durable incarnation.</summary>
    public Guid IncarnationId { get; }
    /// <summary>Gets the original envelope delivery identity.</summary>
    public string DeliveryId { get; }
}
