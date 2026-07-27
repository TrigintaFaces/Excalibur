// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Enforces the outbox fencing composition invariant at startup: when a leader gate is active
/// (a leader election is registered and the consumer has not opted out via
/// <c>OutboxDeliveryOptions.SingleActiveWriter</c>), the configured store MUST be able to enforce a
/// fencing high-water mark (<see cref="IFencedOutboxStore"/>). Otherwise a superseded leader could
/// claim and complete messages it no longer owns.
/// </summary>
/// <remarks>
/// This invariant is enforced at host startup (from the outbox prerequisite validator) so it covers
/// <em>every</em> drain path — including the default background-service → publisher path, which never
/// constructs <see cref="OutboxProcessor"/>. It is also invoked defensively from the
/// <see cref="OutboxProcessor"/> constructor for the partitioned / directly-constructed drain, so a
/// single source of truth governs both. Both callers therefore fail closed identically.
/// </remarks>
internal static class OutboxFencingStartupInvariant
{
	/// <summary>
	/// Throws <see cref="InvalidOperationException"/> when a leader gate is active but the configured
	/// store cannot enforce a fencing high-water mark.
	/// </summary>
	/// <param name="leaderGate"> The registered leader processing gate, or <see langword="null"/> when no leader election is configured. </param>
	/// <param name="singleActiveWriter"> The consumer's explicit single-active-writer opt-out (<c>OutboxDeliveryOptions.SingleActiveWriter</c>). </param>
	/// <param name="outboxStore"> The configured outbox store whose fencing capability is probed via <see cref="IServiceProvider.GetService(Type)"/>. </param>
	/// <exception cref="InvalidOperationException"> Thrown when fencing is active and the store does not implement <see cref="IFencedOutboxStore"/>. </exception>
	public static void EnsureFencingCapableStore(
		ILeaderProcessingGate? leaderGate,
		bool singleActiveWriter,
		IOutboxStore outboxStore)
	{
		ArgumentNullException.ThrowIfNull(outboxStore);

		var fencingActive = leaderGate is not null && !singleActiveWriter;
		if (fencingActive && outboxStore.GetService(typeof(IFencedOutboxStore)) is null)
		{
			throw new InvalidOperationException(
				$"Leader election is configured for the outbox, but the configured store " +
				$"'{outboxStore.GetType().FullName}' does not implement IFencedOutboxStore and therefore cannot " +
				"enforce a fencing high-water mark. A superseded leader would be able to claim and complete " +
				"messages it no longer owns. Use a store that records the high-water durably (e.g. PostgreSQL, Oracle, " +
				"MongoDB, or the in-memory store), or, if exactly one process drains this outbox, opt out " +
				"explicitly with AsSingleWriter() to take responsibility for the single-active-writer guarantee " +
				"yourself. SQL Server also implements IFencedOutboxStore, but derives its high-water from the outbox " +
				"rows rather than a dedicated fence record: a cleanup that purges sent rows resets the mark, and the " +
				"advance overwrites rather than taking the maximum, so the mark can also move backwards. Treat its " +
				"leadership fence as best-effort - the per-message lease still prevents two processors claiming the " +
				"same message. Some stores (e.g. Elasticsearch) cannot express an atomic fencing high-water mark with " +
				"their native primitives and always require AsSingleWriter() under a leader election.");
		}
	}
}
