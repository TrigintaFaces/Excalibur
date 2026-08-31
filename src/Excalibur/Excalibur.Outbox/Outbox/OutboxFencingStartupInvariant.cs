// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Enforces the outbox fencing composition invariant at startup: when a leader election is
/// registered and the consumer has not opted out via <c>OutboxDeliveryOptions.SingleActiveWriter</c>,
/// the drain MUST be fenced by an <see cref="ILeaderProcessingGate"/> AND the configured store MUST be
/// able to enforce a fencing high-water mark (<see cref="IFencedOutboxStore"/>). Otherwise a superseded
/// leader could claim and complete messages it no longer owns.
/// </summary>
/// <remarks>
/// <para>
/// The enabling predicate is the presence of the <em>election</em>, never the presence of the
/// <em>gate</em>. A guard whose enabling condition is supplied by the very component it guards cannot
/// detect the case it exists to detect: a host that registers a leader election through a path that
/// forgets to wire the gate resolves no gate, so a gate-keyed predicate reads "not multi-instance" and
/// passes silently — while every instance drains unfenced, on a deployment whose operator registered an
/// election precisely to prevent that. Keying on the election makes the missing gate a startup refusal
/// instead of a silent downgrade.
/// </para>
/// <para>
/// This invariant is enforced at host startup (from the outbox prerequisite validator) so it covers
/// <em>every</em> drain path — including the default background-service → publisher path, which never
/// constructs <see cref="OutboxProcessor"/>. It is also invoked defensively from the
/// <see cref="OutboxProcessor"/> constructor for the partitioned / directly-constructed drain, so a
/// single source of truth governs both. Both callers therefore fail closed identically.
/// </para>
/// </remarks>
internal static class OutboxFencingStartupInvariant
{
	/// <summary>
	/// Throws <see cref="InvalidOperationException"/> when a leader election is registered without the
	/// single-active-writer opt-out and either the drain is not gated or the configured store cannot
	/// enforce a fencing high-water mark.
	/// </summary>
	/// <param name="electionRegistered"> Whether an <c>ILeaderElection</c> is registered in the container — the multi-instance signal, and the enabling predicate for fencing. </param>
	/// <param name="leaderGate"> The registered leader processing gate, or <see langword="null"/> when the drain is not fenced. </param>
	/// <param name="singleActiveWriter"> The consumer's explicit single-active-writer opt-out (<c>OutboxDeliveryOptions.SingleActiveWriter</c>). </param>
	/// <param name="outboxStore"> The configured outbox store whose fencing capability is probed via <see cref="IServiceProvider.GetService(Type)"/>. </param>
	/// <exception cref="InvalidOperationException"> Thrown when fencing is active and either no leader gate is registered or the store does not implement <see cref="IFencedOutboxStore"/>. </exception>
	public static void EnsureFencingCapableStore(
		bool electionRegistered,
		ILeaderProcessingGate? leaderGate,
		bool singleActiveWriter,
		IOutboxStore outboxStore)
	{
		ArgumentNullException.ThrowIfNull(outboxStore);

		// EITHER signal enables fencing. The gate alone was the old predicate and is retained, but it
		// cannot be the only one: it is supplied by the very component it guards, so a host whose election
		// was registered through a path that never wired the gate would read as single-instance and start
		// silently, unfenced. Adding the election as an independent signal is monotonic — it can only
		// turn a silent pass into a refusal, never a refusal into a pass.
		var fencingActive = (electionRegistered || leaderGate is not null) && !singleActiveWriter;
		if (!fencingActive)
		{
			return;
		}

		if (leaderGate is null)
		{
			throw new InvalidOperationException(
				"A leader election is registered, but the outbox drain is not fenced: no ILeaderProcessingGate " +
				"resolved from the container. Every instance would drain the outbox concurrently, so the " +
				"coordination guarantee the leader election was added to provide would not hold. Wire the gate " +
				"with the outbox builder's WithLeaderElection(), or, if exactly one process drains this outbox, " +
				"opt out explicitly with AsSingleWriter() to take responsibility for the single-active-writer " +
				"guarantee yourself.");
		}

		if (outboxStore.GetService(typeof(IFencedOutboxStore)) is null)
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

	/// <summary>
	/// Determines whether an <see cref="ILeaderElection"/> is registered in the container — the
	/// multi-instance signal this invariant keys on.
	/// </summary>
	/// <param name="services"> The application service provider. </param>
	/// <returns> <see langword="true"/> when an <see cref="ILeaderElection"/> is registered; otherwise <see langword="false"/>. </returns>
	/// <remarks>
	/// Asks <see cref="IServiceProviderIsService"/> rather than resolving the service, so a host that
	/// registers a leader election does not construct it (and open its backing connection) merely to be
	/// validated. AOT-safe: a typed probe, no reflection and no assembly scanning. Containers that do not
	/// implement <see cref="IServiceProviderIsService"/> fall back to a resolution probe rather than
	/// assuming the safer-looking answer, so a third-party container cannot silently disable the guard.
	/// The probe is deliberately non-keyed: the leader gate resolves <see cref="ILeaderElection"/>
	/// non-keyed, and every provider registration path aliases its keyed registration onto the non-keyed
	/// contract, so this is exactly the registration the gate would consume.
	/// </remarks>
	public static bool IsLeaderElectionRegistered(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Deliberately GetService(Type) + a type-test rather than the generic GetService<T>() extension:
		// the generic form performs a hard cast, so any provider that answers an unknown service type with a
		// non-null value of the wrong type (a test double, a permissive third-party container) would throw
		// InvalidCastException out of a startup guard. A type-test degrades to "not registered" instead.
		return services.GetService(typeof(IServiceProviderIsService)) is IServiceProviderIsService isService
			? isService.IsService(typeof(ILeaderElection))
			: services.GetService(typeof(ILeaderElection)) is ILeaderElection;
	}
}
