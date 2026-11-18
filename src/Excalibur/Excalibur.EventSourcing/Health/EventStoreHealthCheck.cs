// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check that verifies the event store can serve a read of its event table — not merely that its
/// database accepts a connection.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this establishes, stated so it can be falsified.</b> The probe reads the first event of the
/// global stream. A relational provider validates a statement's select list at <i>parse</i> time, so the
/// probe detects an event table that is present but missing a column the provider's read statement
/// binds, <b>on an empty database with zero rows</b>. It needs no data to be meaningful.
/// </para>
/// <para>
/// <b>Whether it detects an ABSENT table depends on the provider, and on one of them it does not.</b>
/// Measured across the five shipped providers that supply a global-stream reader: the SQL Server,
/// PostgreSQL, Oracle and in-memory readers go straight to their select, so an absent table fails and
/// the check reports Unhealthy. The SQLite reader provisions the table on its read path before
/// selecting, so an absent table is CREATED and the check reports Healthy -- truthfully, because a read
/// genuinely was served, but a deployment that never provisioned its schema is not detected there.
/// That is a property of that provider's read path rather than of this check, and it is stated here
/// because the check is the surface an operator reads.
/// </para>
/// <para>
/// <b>There are exactly two verdicts, and the verdict is decided by the STORE.</b>
/// <see cref="HealthStatus.Healthy" /> means the read was served. <see cref="HealthStatus.Unhealthy" />
/// means the store refused a read it should have served. Cancellation propagates rather than being
/// reported as a fault, so a host shutting down does not raise a false alarm. There is deliberately no
/// third verdict for "the probe could not be taken": the probe depends on no ambient tenant, no shard
/// map and no default shard, so no configuration can make it unavailable. A composition that cannot
/// supply a WORKING global-stream reader — one that is not registered at all, or one registered
/// through a factory that cannot construct it — is refused at startup instead of being handed a check
/// whose green would mean something else. The distinction matters because a registration can exist
/// and still be unbuildable, and that case used to reach the first scrape.
/// </para>
/// <para>
/// <b>Why the global-stream read rather than an aggregate load.</b> An earlier version loaded a reserved
/// aggregate, on the stated grounds that an aggregate load binds "a strict superset" of what the
/// global-stream read binds. That was measured and it is false: the two statements select the SAME
/// eleven columns, and what the aggregate load adds is predicates, not columns. It did however bind a
/// tenant term, so on a tenant-routing deployment with no default shard the probe could not resolve a
/// store at all and the check reported a non-verdict on every single invocation — permanently, since
/// nothing about that configuration varies per call. The global-stream read binds the same columns with
/// no tenant or shard dependency, which is why it costs nothing to prefer it.
/// </para>
/// <para>
/// <b>What this does NOT establish — read this before relying on a green.</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>The write path.</b> The probe appends nothing, so the append statement's column list is never
///     bound: a column missing only on the write side stays green here and fails on the first real
///     append. Note that "appends nothing" is narrower than "has no side effect" — the SQLite reader
///     provisions the events table on its read path, so on that provider a probe against an
///     unprovisioned database issues DDL. No provider's probe writes an event.
///   </item>
///   <item>
///     <b>Row materialization, ON AN EMPTY STORE.</b> With zero rows the mapping from columns to the
///     returned type never runs, so a column present with the wrong type parses, passes here, and
///     throws on the first real load. On a non-empty store one row IS materialized, so for that one
///     row the mapping is established — this limitation is the empty-database case, not the general
///     one, and it is the case a freshly provisioned deployment is in.
///   </item>
///   <item>
///     <b>Any other schema object.</b> The snapshot table, the projection and materialized-view tables
///     and their position tables, the inbox and outbox tables are separate objects with separate
///     statements. This probe reads one table.
///   </item>
///   <item>
///     <b>The schema of the shard a tenant's request would route to.</b> The global-stream reader is
///     bound to the primary connection, so on a sharded deployment this verifies the event table of
///     whichever store that connection names. If that store is also a shard, that shard is verified
///     incidentally; the shard a given tenant routes to is not.
///   </item>
///   <item>
///     <b>Anything about the other shards, and no other shipped check establishes it either.</b> The
///     separate per-shard check enumerates the shard map and resolves a store for each entry. It
///     issues no read and opens no connection — the store it builds holds a connection factory, not a
///     connection — so it establishes that each entry's configuration is complete, and not that the
///     shard answers or that its schema is usable. It establishes even that only once: stores are
///     cached per shard, so every invocation after the first returns a cached object and re-validates
///     nothing.
///   </item>
///   <item>
///     <b>Independence from cold storage.</b> Where tiered storage is registered, the global-stream
///     reader hydrates a row whose PAYLOAD has been moved to cold storage — which is narrower than
///     "archived", because a row can be marked archived and still carry its payload, and such a row
///     needs no cold read. So this probe reaches the archive only when the first event's payload is
///     absent, and an unavailable archive is then reported as an unhealthy event store.
///   </item>
/// </list>
/// </remarks>
internal sealed class EventStoreHealthCheck : IHealthCheck
{
	private readonly IGlobalStreamQuery _globalStream;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreHealthCheck" /> class.
	/// </summary>
	/// <param name="globalStream">The global-stream reader to probe.</param>
	public EventStoreHealthCheck(IGlobalStreamQuery globalStream)
	{
		_globalStream = globalStream ?? throw new ArgumentNullException(nameof(globalStream));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			// One row is enough: the select list is bound at parse time, so a missing table or a missing
			// column fails here even against an empty store. Reading more would cost IO and prove nothing
			// extra.
			var events = await _globalStream
				.ReadAllAsync(GlobalStreamPosition.Start, 1, cancellationToken)
				.ConfigureAwait(false);

			return HealthCheckResult.Healthy(
				$"Event store served a read of its event table ({events.Count} row(s) returned), so that "
				+ "table and the columns its read statement binds exist.");
		}
		catch (OperationCanceledException)
		{
			// A host shutting down is not an unhealthy store. Let the framework observe the cancellation
			// rather than converting a rolling deploy into a page.
			throw;
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Event store refused a read it should have served. Its event table may be absent, or "
				+ $"present but missing a column the provider's read statement binds: {ex.Message}",
				exception: ex);
		}
	}
}
