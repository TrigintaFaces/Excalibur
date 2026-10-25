// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Preserves commit certainty across a complete append, including its cleanup, so that
/// <see cref="AppendOutcome.Failed"/> cannot be reported for an append that may have committed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IEventStore.AppendAsync"/> permits <see cref="AppendOutcome.Failed"/> — which states
/// that nothing was written — only where non-commit is <b>established</b>. The difficulty is that the
/// place a store learns it failed is usually a single exception handler spanning the whole operation,
/// with no record of whether the commit was ever dispatched. A handler in that position cannot tell a
/// fault that preceded the commit from a commit that landed and lost its acknowledgement, so it cannot
/// support the claim — and a caller told "nothing was written" does not reconcile, which is how a
/// durable write becomes invisible to everything downstream.
/// </para>
/// <para>
/// This type closes that by construction rather than by discipline. Wrap the append in
/// <see cref="ExecuteAsync"/>, call <see cref="MarkDispatched"/> immediately before the commit is sent
/// and <see cref="MarkAcknowledged"/> once it is known durable, then <b>filter the store's own failure
/// handler on <c>!Dispatched</c></b>:
/// </para>
/// <code>
/// catch (Exception ex) when (!state.Dispatched &amp;&amp; ex is MyProviderException)
/// {
///     return AppendResult.CreateFailure(ex.Message);
/// }
/// </code>
/// <para>
/// With that filter the <see cref="AppendOutcome.Failed"/> arm is only <i>reachable</i> when the commit
/// was never dispatched, so non-commit is established wherever it is returned and no author has to
/// remember the rule. Anything that escapes after dispatch is converted here to
/// <see cref="AppendOutcome.Unknown"/>, which is the outcome defined for a commit whose fate cannot be
/// determined. A store that can probe for its own committed events — reading them back by identity — is
/// strictly better off doing so first, because a probe can distinguish landed from taken from
/// indeterminate; this boundary is the floor beneath that, for the paths a probe cannot reach and for
/// stores that have none.
/// </para>
/// <para>
/// The state transitions are deliberately one-way. There is no setter that can un-dispatch an append,
/// because the only use for one would be to re-open the hole this type exists to close.
/// </para>
/// </remarks>
public sealed class AppendCommitBoundary
{
	// Only ExecuteAsync may create one. A boundary built by hand is attached to no operation, so
	// marking it would record a dispatch nothing observes -- a construction with no correct use.
	private AppendCommitBoundary()
	{
	}

	/// <summary>
	/// Gets a value indicating whether the commit has been dispatched to the provider.
	/// </summary>
	/// <value>
	/// <see langword="true"/> once <see cref="MarkDispatched"/> or <see cref="MarkAcknowledged"/> has been
	/// called; otherwise <see langword="false"/>. Filter a failure handler that reports
	/// <see cref="AppendOutcome.Failed"/> on this being <see langword="false"/>.
	/// </value>
	public bool Dispatched { get; private set; }

	/// <summary>
	/// Gets the result of a commit already known to be durable, if one has been acknowledged.
	/// </summary>
	/// <value>
	/// The acknowledged result, or <see langword="null"/> while no commit is known durable.
	/// </value>
	public AppendResult? Acknowledged { get; private set; }

	/// <summary>
	/// Runs an append inside the boundary, converting any post-dispatch fault into
	/// <see cref="AppendOutcome.Unknown"/> rather than letting it be reported as a failure.
	/// </summary>
	/// <param name="operation">The append, which receives the boundary state to mark.</param>
	/// <returns>
	/// The operation's own result; the acknowledged result if a durable commit was followed by a fault in
	/// cleanup; or <see cref="AppendOutcome.Unknown"/> if a fault followed dispatch.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">An ambient transaction is in scope.</exception>
	public static async ValueTask<AppendResult> ExecuteAsync(
		Func<AppendCommitBoundary, ValueTask<AppendResult>> operation)
	{
		ArgumentNullException.ThrowIfNull(operation);

		// An ambient transaction would place commit certainty outside the store, where none of the
		// guarantees below can be stated: the store would report an outcome the enclosing transaction
		// could still reverse.
		if (System.Transactions.Transaction.Current is not null)
		{
			throw new InvalidOperationException(
				"Event store appends require a store-owned transaction and cannot run inside an ambient transaction.");
		}

		var state = new AppendCommitBoundary();
		try
		{
			return await operation(state).ConfigureAwait(false);
		}
		catch (Exception) when (state.Acknowledged is not null)
		{
			// This boundary includes logging, telemetry and asynchronous resource disposal. An
			// acknowledged durable commit cannot become a rejected append because something after it
			// threw.
			return state.Acknowledged;
		}
		catch (OperationCanceledException)
		{
			// Cancellation after dispatch is intentionally not a statement of rollback. Reporting a
			// result here would invite a retry inside a scope that is already cancelled.
			throw;
		}
		catch (Exception) when (state.Dispatched)
		{
			return AppendResult.CreateUnknown(
				"Commit was requested but its outcome could not be established. Reconcile the original operation.");
		}
	}

	/// <summary>
	/// Records that the commit has been sent to the provider. Call immediately before dispatching it.
	/// </summary>
	/// <remarks>
	/// Idempotent and one-way. From this point a fault can no longer establish non-commit, which is
	/// exactly what the <c>!Dispatched</c> filter on the store's failure handler relies on.
	/// </remarks>
	public void MarkDispatched() => Dispatched = true;

	/// <summary>
	/// Records a commit now known to be durable, so no later fault can downgrade it.
	/// </summary>
	/// <param name="result">The successful append result to preserve.</param>
	/// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="result"/> does not report success.</exception>
	/// <remarks>
	/// Implies <see cref="Dispatched"/>: a commit cannot be acknowledged without having been sent.
	/// Refusing a non-success result is deliberate — "acknowledged" names a durable commit, and
	/// admitting a failure here would let a later fault return that failure as though it had been
	/// established.
	/// </remarks>
	public void MarkAcknowledged(AppendResult result)
	{
		ArgumentNullException.ThrowIfNull(result);

		if (!result.Success)
		{
			throw new ArgumentException(
				"Only a successful append may be acknowledged: this records a commit already known to be durable.",
				nameof(result));
		}

		Dispatched = true;
		Acknowledged = result;
	}
}
