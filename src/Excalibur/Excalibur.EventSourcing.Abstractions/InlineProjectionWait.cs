// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Suspends the read-your-write wait that an inline projection normally performs on every save, for a
/// bulk writer that will drive the projection cursor itself once at the end of its batch.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cost this exists to remove.</b> An inline projection advances event by event, because each save
/// waits for the projection cursor to reach the position that save just committed. That is the correct
/// price for a request that writes and then reads its own write. It is the wrong price for a bulk writer —
/// a change-data-capture backfill, an import, a replay — which writes thousands of aggregates and reads
/// none of them back. There the per-save wait is paid in full, serially, and any per-page cost on the
/// projection side becomes a per-event cost: a projection store configured to make each page searchable
/// before its checkpoint advances performs one full index refresh per event, and the refresh becomes the
/// throughput limit for the whole job.
/// </para>
/// <para>
/// <b>What it does and does not change.</b> Inside the scope, a save still commits its events durably and
/// still returns; only the foreground WAIT is skipped, and the save reports
/// <see cref="ProjectionFreshness.Deferred"/> so a deferred wait can never be mistaken for a confirmed
/// one. The events remain queued for the inline cursor exactly as before. Nothing about durability,
/// ordering or the projection's eventual content changes.
/// </para>
/// <para>
/// <b>The obligation it transfers to you.</b> Something must still advance the cursor. In a host that runs
/// hosted services the inline background loop does it, so the projection catches up on its own. In a
/// pull-driven host — a function, a worker that runs no hosted services, a console job — nothing does,
/// and the caller MUST drive it: call <see cref="IProjectionProcessor.ProcessPendingAsync"/> once after
/// the batch. That single call folds everything the batch committed in pages, so a per-page cost is paid
/// per page rather than per event, which is the whole saving.
/// </para>
/// <para>
/// <b>Do not wrap a request handler in this.</b> If any code inside the scope reads a projection it
/// expects its own write to appear in, it will not see it. The scope is for a writer that reads nothing
/// back. It is ambient and flows to the work started inside it, so it covers a handler called within the
/// scope without that handler knowing — which is what makes it usable around existing code, and also why
/// its extent should be kept as narrow as the batch.
/// </para>
/// <example>
/// A change-data-capture batch, suppressing per-record waits and folding once at the end:
/// <code>
/// using (InlineProjectionWait.Suppress())
/// {
///     foreach (var record in batch)
///     {
///         await handler.ProcessAsync(record, cancellationToken);
///     }
/// }
///
/// // One fold for the whole batch, in pages, instead of one per record.
/// var result = await processor.ProcessPendingAsync(cancellationToken);
/// </code>
/// </example>
/// <para>
/// Modelled on <see cref="System.Threading.ExecutionContext.SuppressFlow"/>: an ambient, scoped,
/// nestable suppression whose disposal restores exactly the state that preceded it.
/// </para>
/// </remarks>
public static class InlineProjectionWait
{
	private static readonly AsyncLocal<int> Depth = new();

	/// <summary>
	/// Gets a value indicating whether the inline read-your-write wait is currently suppressed on this
	/// execution context.
	/// </summary>
	/// <value>
	/// <see langword="true"/> while at least one <see cref="Suppress"/> scope is open on this context;
	/// otherwise <see langword="false"/>.
	/// </value>
	public static bool IsSuppressed => Depth.Value > 0;

	/// <summary>Suppresses the inline read-your-write wait until the returned scope is disposed.</summary>
	/// <returns>A scope that restores the previous suppression state when disposed.</returns>
	/// <remarks>
	/// <para>
	/// Nestable: an inner scope's disposal restores the outer scope rather than clearing suppression, so a
	/// batch that calls into code which itself suppresses is not silently un-suppressed partway through.
	/// Disposal is idempotent.
	/// </para>
	/// <para>
	/// The concrete scope type is deliberately not public — the same shape as
	/// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}"/>, whose scope is an opaque
	/// <see cref="IDisposable"/>. A caller has nothing to do with the scope but dispose it, so exposing a
	/// named type would add public surface carrying no consumer decision.
	/// </para>
	/// </remarks>
	public static IDisposable Suppress()
	{
		var previous = Depth.Value;
		Depth.Value = previous + 1;
		return new Scope(previous);
	}

	private sealed class Scope(int previous) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Depth.Value = previous;
		}
	}
}
