// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Features;

/// <summary>
/// Carries an inbox lease across the dispatch boundary in both directions: the term a caller already
/// holds on an entry, and what the inbox deduplication stage subsequently did with the dispatch.
/// </summary>
/// <remarks>
/// <para>
/// A caller that has already acquired a lease on an entry and is dispatching it — a drain, typically —
/// sets <see cref="MessageId"/>, <see cref="HandlerType"/> and <see cref="Lease"/> before dispatching.
/// The inbox stage recognises that term as its own caller's and admits the invocation instead of
/// treating the holder as a competitor; without it the stage asks the store for a lease the caller is
/// already holding, is refused, and silently skips the handler.
/// </para>
/// <para>
/// An invocation admitted on a term it did not mint is a BORROWER. It runs the handler and returns the
/// real result, and it finalizes nothing: the claimant that minted the term owns both the completion
/// and the failure, because that is where attempt accounting lives. A borrower that recorded a failure
/// would clear the term the claimant is about to finalize under, and the attempt would be recorded by
/// nobody.
/// </para>
/// <para>
/// On return, <see cref="Disposition"/> reports what the stage did, so the claimant can tell an
/// invocation that ran from one that was refused. The two are otherwise indistinguishable — a refusal
/// and a completion both surface as a successful dispatch.
/// </para>
/// <para>
/// The term is an admission credential here, not merely an identity, so a store whose lease term is
/// predictable — a timestamp, an expiry, a counter — does not meet the contract this relies on. See the
/// unguessability requirement on the leased-store contract.
/// </para>
/// <para>
/// This feature is deliberately NOT propagated to child contexts: a term admits an invocation for one
/// specific entry and must not travel to dispatches it was never issued for. Keep it that way.
/// </para>
/// </remarks>
public interface IInboxLeaseFeature
{
	/// <summary>
	/// Gets or sets the store identifier of the entry the term was issued for.
	/// </summary>
	/// <value>The entry identifier, or <see langword="null"/> when no term is carried.</value>
	string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified handler type name the term was issued for.
	/// </summary>
	/// <value>The handler type name, or <see langword="null"/> when no term is carried.</value>
	string? HandlerType { get; set; }

	/// <summary>
	/// Gets or sets the lease term the dispatching caller already holds on the entry.
	/// </summary>
	/// <value>The held term, or <see langword="null"/> when the caller holds no lease.</value>
	LeaseToken? Lease { get; set; }

	/// <summary>
	/// Gets or sets what the inbox deduplication stage did with this dispatch.
	/// </summary>
	/// <value>The disposition; <see cref="InboxLeaseDisposition.NotEvaluated"/> until the stage runs.</value>
	InboxLeaseDisposition Disposition { get; set; }
}
