// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents a scheduled timeout for a saga instance. Timeouts are used to trigger
/// actions after a specified delay when a saga is waiting for external events.
/// </summary>
/// <param name="TimeoutId">Unique identifier for this timeout instance.</param>
/// <param name="SagaId">Identifier of the saga that requested this timeout.</param>
/// <param name="SagaType">Assembly-qualified type name of the saga for routing.</param>
/// <param name="TimeoutType">Assembly-qualified type name of the timeout message type.</param>
/// <param name="TimeoutData">Serialized timeout data (MemoryPack format), or null for parameterless timeouts.</param>
/// <param name="DueAt">UTC timestamp when this timeout should be delivered.</param>
/// <param name="ScheduledAt">UTC timestamp when this timeout was originally scheduled.</param>
public sealed record SagaTimeout(
	string TimeoutId,
	string SagaId,
	string SagaType,
	string TimeoutType,
	byte[]? TimeoutData,
	DateTimeOffset DueAt,
	DateTimeOffset ScheduledAt)
{
	/// <summary>
	/// Gets the tenant that owns the saga this timeout belongs to, as persisted on the timeout row — or the
	/// reserved untenanted term for a saga that is not tenant-scoped. Never <see langword="null"/>.
	/// </summary>
	/// <value>The owning tenant term, or the reserved untenanted term.</value>
	/// <remarks>
	/// <para>
	/// <strong>Populated by the store when a timeout is read back; ignored when one is scheduled.</strong>
	/// A caller scheduling a timeout does not supply it — the store stamps the row from the ambient tenant, the
	/// same authority every other store in this framework uses, so a scheduled timeout cannot claim a tenant its
	/// caller had not established.
	/// </para>
	/// <para>
	/// It is carried back on the read because timeout delivery is a background, estate-wide operation: the
	/// delivery loop leases due timeouts across every tenant in one batch — it cannot itself be tenant-scoped
	/// without claiming nothing — and must then re-establish each timeout's own tenant before dispatching it, so
	/// the saga the handler loads is the saga that scheduled it. A timeout dispatched without its tenant
	/// re-established resolves a different partition than the one the saga was saved under, finds nothing, and
	/// fails silently.
	/// </para>
	/// </remarks>
	public string TenantId { get; init; } = KeyedTenantPartition.Untenanted.TenantId;
}
