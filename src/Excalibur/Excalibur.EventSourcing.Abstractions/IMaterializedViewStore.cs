// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the contract for materialized view persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// Materialized views are read-optimized projections built from event streams.
/// This interface provides the storage operations for view state.
/// </para>
/// <para>
/// For building views from events, use <see cref="IMaterializedViewBuilder{TView}"/>.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for synchronous completions (e.g., in-memory stores, cache hits). Callers should await the result
/// immediately and not store the ValueTask for later use.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every operation is confined to the ambient tenant established for this
/// store instance, and that includes the checkpoint: <see cref="GetAsync{TView}"/> reports a view stored
/// under another tenant as not found, <see cref="SaveAsync{TView}"/> can neither read nor overwrite
/// another tenant's view under the same <c>viewName</c> and <c>viewId</c>, <see cref="DeleteAsync"/>
/// cannot remove one, and <see cref="GetPositionAsync"/> and <see cref="SavePositionAsync"/> read and
/// write a position owned by this tenant alone. The confinement applies to writes and deletes as well as
/// reads: scoping only the read paths turns a disclosure into silent data loss rather than removing the
/// problem. A position is the sharper case of the same rule -- a checkpoint keyed on view name alone holds
/// one position for every tenant, so one tenant's progress advances another's and that tenant's projector
/// skips every event in between, which is data loss that reports success.
/// </para>
/// <para>
/// Every implementation shipped with the framework holds that boundary, by carrying the tenant term in the
/// statement or in the document identifier rather than filtering after the fact. The framework does not
/// gate this contract at registration, however, so a store presenting no capability marker
/// -- <see cref="Excalibur.Dispatch.ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant -- is not
/// confined by the framework, only by its own statements.
/// </para>
/// </remarks>
public interface IMaterializedViewStore
{
	/// <summary>
	/// Gets a materialized view by its identifier.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The view if found, or null if not found.</returns>
	[RequiresUnreferencedCode("Implementations serialize the view type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming.")]
	[RequiresDynamicCode("Implementations serialize the view type reflectively; supply JsonSerializerOptions with a source-generated resolver for AOT.")]
	ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
		where TView : class;

	/// <summary>
	/// Saves a materialized view.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="view">The view to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RequiresUnreferencedCode("Implementations serialize the view type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming.")]
	[RequiresDynamicCode("Implementations serialize the view type reflectively; supply JsonSerializerOptions with a source-generated resolver for AOT.")]
	ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
		where TView : class;

	/// <summary>
	/// Deletes a materialized view.
	/// </summary>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// Confined to the ambient tenant, like every other operation on this store: a view stored under
	/// another tenant with the same <paramref name="viewName"/> and <paramref name="viewId"/> is not
	/// deleted, and the call succeeds silently because that view is not visible to this caller. An
	/// implementation that omits the tenant term here deletes another tenant's view -- silent data loss
	/// rather than a disclosure, and the projector that rebuilt it will not notice.
	/// </para>
	/// </remarks>
	ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the last processed position for a view.
	/// </summary>
	/// <param name="viewName">The view name for position tracking.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The last processed position, or null if no position has been recorded.</returns>
	/// <remarks>
	/// <para>
	/// The position is used to track which events have been processed for a view,
	/// enabling catch-up subscriptions and rebuild scenarios.
	/// </para>
	/// </remarks>
	ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken);

	/// <summary>
	/// Advances the last processed position for a view, and reports whether the advance was taken.
	/// </summary>
	/// <param name="viewName">The view name for position tracking.</param>
	/// <param name="position">The position to advance to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ViewPositionSaveOutcome.Advanced"/> when the checkpoint moved to
	/// <paramref name="position"/>; <see cref="ViewPositionSaveOutcome.RefusedAsStale"/> when the stored
	/// checkpoint was already at or beyond it and nothing was written.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>This advance is MONOTONIC in every implementation, and the refusal is a normal outcome rather
	/// than an error.</b> A delayed or retried write carrying an older position must not rewind the
	/// checkpoint, because the projection would then replay events it has already applied. Every store
	/// enforces that server-side.
	/// </para>
	/// <para>
	/// <b>The return value exists because the refusal used to be unobservable.</b> This member returned
	/// <see cref="ValueTask"/> — a caller could not tell a taken advance from a refused one, and every
	/// implementation logged a successful save either way. That hid a real defect for as long as the
	/// monotonic guard existed: a caller that needed the position to go BACKWARDS was silently refused and
	/// reported success. An operation that can decline to act must say so in its signature.
	/// </para>
	/// <para>
	/// <b>To move a checkpoint backwards, use <see cref="ResetPositionAsync"/>.</b> Lowering a position is
	/// a different operation from advancing one, so it is a different member rather than a flag — this
	/// method cannot express it and must not be asked to.
	/// </para>
	/// </remarks>
	ValueTask<ViewPositionSaveOutcome> SavePositionAsync(
		string viewName,
		long position,
		CancellationToken cancellationToken);

	/// <summary>
	/// Clears the recorded position for a view, so the next read reports no checkpoint at all.
	/// </summary>
	/// <param name="viewName">The view name for position tracking.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// <b>Unconditional by contract.</b> This is the operation a rebuild needs, and it is deliberately not
	/// subject to the monotonic guard that governs <see cref="SavePositionAsync"/> — a guard whose whole
	/// purpose is to refuse exactly this.
	/// </para>
	/// <para>
	/// <b>It clears rather than zeroes.</b> <see cref="GetPositionAsync"/> already returns
	/// <see langword="null"/> for a view with no checkpoint, so absence is the existing representation of
	/// "start from the beginning" and needs no second encoding. Writing zero would also be indistinguishable
	/// from a view legitimately checkpointed at position zero.
	/// </para>
	/// <para>
	/// <b>It returns no outcome because it cannot refuse.</b> Clearing an already-absent checkpoint is
	/// success, not a refusal. A store that cannot clear throws.
	/// </para>
	/// </remarks>
	ValueTask ResetPositionAsync(string viewName, CancellationToken cancellationToken);

	// An atomic view+position write is deliberately NOT declared here, not even as a virtual member with a
	// sequential default. A default would let a store inherit an exactly-once guarantee it cannot honour,
	// and the two-write fallback silently double-counts accumulating views after a crash. Stores that can
	// commit both writes together implement IAtomicMaterializedViewStore and say so in the type system.
}


/// <summary>
/// Whether a view-position advance was taken or refused as stale.
/// </summary>
public enum ViewPositionSaveOutcome
{
	/// <summary>
	/// The checkpoint moved to the requested position.
	/// </summary>
	Advanced,

	/// <summary>
	/// The stored checkpoint was already at or beyond the requested position, so the advance was refused
	/// and nothing was written.
	/// </summary>
	/// <remarks>
	/// This is the expected outcome for a delayed or retried write that lost a race, and for any caller
	/// asking the checkpoint to move backwards — the monotonic guard working as designed. A caller that
	/// INTENDED to lower the position wanted <see cref="IMaterializedViewStore.ResetPositionAsync"/> and
	/// must treat this as a failure of its own operation rather than ignoring it.
	/// </remarks>
	RefusedAsStale,
}
