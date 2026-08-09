// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing;

namespace Excalibur.MultiTenancy;

/// <summary>
/// The authoritative set of persistence contracts that store tenant-owned rows.
/// </summary>
/// <remarks>
/// <para>
/// This manifest exists so that "which contracts must be tenant-gated" is a <em>value</em> rather than
/// control flow. Before it existed, the set was expressed only as a sequence of hand-written
/// <c>if (services.Any(...))</c> blocks inside the row-discriminator wiring, and a set expressed as control
/// flow cannot be enumerated: nothing can assert that a newly added tenant-owned contract was actually gated,
/// because there is no set to compare against.
/// </para>
/// <para>
/// It also gives the guard test an oracle that is <em>independent of the artifact under test</em>. A test that
/// scrapes the required contracts out of the gate's own source can be satisfied by deleting a line from the
/// gate: the expected set shrinks to match the actual set and the assertion passes while the protection is
/// gone. Comparing the gate against this manifest removes that degree of freedom.
/// </para>
/// <para>
/// Gating a contract in <c>ApplyRowDiscriminator</c> that is absent from this manifest fails the boundary
/// guard, whose oracle is this list.
/// </para>
/// <para>
/// The reverse direction is <em>not</em> covered, and the difference matters. Adding a contract here without
/// gating it, and omitting a tenant-owned contract from this list altogether, are both currently silent.
/// <c>ApplyRowDiscriminator</c> carries no runtime exhaustiveness assertion — an earlier revision's count
/// check was removed deliberately, because it derived its expected set from the very method it was meant to
/// constrain and could be satisfied by deleting a line from both sides at once.
/// </para>
/// <para>
/// So this manifest's own completeness is unbounded: a tenant-owned persistence contract that never appears
/// here is invisible to every check that takes the manifest as its expected set, including the boundary
/// guard. Closing that requires a guard whose oracle is the namespace rather than this list. Until then,
/// treat membership as a maintained assertion rather than an enforced one, and do not infer a control here
/// that does not exist.
/// </para>
/// </remarks>
internal static class TenantOwnedContracts
{
	/// <summary>
	/// Gets every contract whose rows belong to a tenant and which must therefore be covered by the
	/// tenant-scoping capability gate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="IInboxStore"/> and <see cref="IOutboxStore"/> are gated but deliberately <b>not decorated</b>.
	/// The outbox drain is intentionally cross-tenant — it establishes a per-message tenant scope as it drains,
	/// because one pass carries every tenant's messages — so a tenant-scoped decorator would read the ambient
	/// tenant as absent, claim the empty set, and stall the drain permanently while still satisfying a
	/// safety-only test. The inbox already applies the tenant predicate inside its provider stores, on the
	/// composite (tenant, message, handler) key; wrapping a store that already filters would add a second filter
	/// without repairing the first.
	/// </para>
	/// <para>
	/// <see cref="IEventStoreErasure"/> is gated and <b>not decorated</b>, for a reason unique to it: erasure
	/// runs from a background service with no ambient tenant established. A tenant-scoping decorator would
	/// therefore read the tenant as absent and either refuse every erasure or widen it to all tenants — and the
	/// widened form is the live defect, because the erase request drops its tenant predicate when the
	/// discriminator is null, tombstoning every tenant's copy of an aggregate in response to one tenant's
	/// right-to-erasure request.
	/// </para>
	/// <para>
	/// <see cref="IErasureStore"/> and <see cref="ILegalHoldStore"/> are gated and <b>not decorated</b>, for
	/// the same reason as the inbox: each provider applies the ambient tenant predicate inside its own store,
	/// so wrapping one that already filters would add a second filter without repairing the first. Their
	/// estate-wide background surfaces — the erasure scheduler's due-request drain, the certificate retention
	/// sweep, and the legal-hold expiry sweep — are deliberately unscoped and live on the separate query and
	/// certificate contracts, which a per-tenant caller does not depend on. A decorator over the store
	/// contract could not make that distinction and would either stall those sweeps or widen the reads.
	/// </para>
	/// </remarks>
	internal static readonly ImmutableArray<Type> All =
	[
		typeof(IEventStore),
		typeof(IProjectionStore<object>),
		typeof(ISagaStore),
		typeof(IInboxStore),
		typeof(IOutboxStore),
		typeof(IEventStoreErasure),
		typeof(IErasureStore),
		typeof(ILegalHoldStore),
	];
}
