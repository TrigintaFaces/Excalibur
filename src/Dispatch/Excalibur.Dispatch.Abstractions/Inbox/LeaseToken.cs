// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An opaque ownership term identifying one holder of a lease on an inbox entry, returned by
/// <see cref="ILeasedInboxStore.TryAcquireLeaseAsync"/> and presented back on every later write.
/// </summary>
/// <remarks>
/// <para>
/// The value is <b>chosen and written by the store</b>, never computed by the caller, and is compared
/// only for equality inside the store's own atomic step. Callers MUST treat it as opaque: do not parse
/// it, order it, or derive meaning from it. Each store picks whatever term it already has — a
/// server-written expiry, a document revision, a sequence-and-primary-term pair — so a store with a
/// native term is never forced to carry a second, redundant one.
/// </para>
/// <para>
/// The term is what makes a lapsed holder harmless. A caller whose lease expired still holds the term
/// of the lease it lost; the holder that replaced it carries a different one, so the lapsed caller's
/// write matches no row and takes no effect. Status alone cannot express this: at the instant of the
/// bad write the entry is legitimately <see cref="InboxStatus.Processing"/> — its successor's — so a
/// status predicate protects the terminal <i>state</i> and not the <i>term</i>.
/// </para>
/// </remarks>
/// <param name="Value">The store-chosen term. Opaque to callers.</param>
public readonly record struct LeaseToken(string Value)
{
	/// <summary>Returns the opaque term. Provided for diagnostics only; never parse this.</summary>
	/// <returns>The store-chosen term value.</returns>
	public override string ToString() => Value;
}
