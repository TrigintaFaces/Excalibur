// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check that verifies the event store can serve a read of its event table — not merely that its
/// database accepts a connection.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this establishes, stated so it can be falsified.</b> The probe loads a reserved, never-written
/// aggregate. A relational provider validates a statement's select list at <i>parse</i> time, so the probe
/// detects an event table that is absent, or present but missing a column the provider's read statement
/// binds, <b>on an empty database with zero rows</b>. It needs no data to be meaningful.
/// </para>
/// <para>
/// <b>What this does NOT establish — read this before relying on a green.</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>The write path.</b> The probe is read-only by design — a health check must never write to the
///     event store — so the append statement's column list is never bound. A column missing only on the
///     write side stays green here and fails on the first real append.
///   </item>
///   <item>
///     <b>Row materialization.</b> The probe returns zero rows, so the mapping from columns to the
///     returned type never runs. A column that is present with the wrong type parses, passes here, and
///     throws on the first real load.
///   </item>
///   <item>
///     <b>Any other schema object.</b> The snapshot table, the projection and materialized-view tables and
///     their position tables, the inbox and outbox tables are separate objects with separate statements.
///     This probe reads one table.
///   </item>
///   <item>
///     <b>Other tenants, other shards, archived payloads.</b> The probe runs with no ambient tenant, so it
///     reads the reserved untenanted partition of whichever store the current configuration resolves. It
///     does not sweep shards, and it returns no archived rows, so cold storage is never exercised.
///   </item>
/// </list>
/// <para>
/// Schema correctness is a deployment property, and the honest place to settle it is host startup, where a
/// mis-provisioned deployment can refuse to start instead of serving traffic while reporting itself
/// reachable. This check is the runtime floor beneath that, not a substitute for it.
/// </para>
/// <para>
/// <b>Status semantics.</b> <see cref="HealthStatus.Unhealthy" /> means the store refused a read it should
/// have served. <see cref="HealthStatus.Degraded" /> means the probe could not be taken at all — a
/// configuration state, not a store fault — and is reported rather than silently passed, because a check
/// that cannot distinguish "did not run" from "passed" is not a check. Cancellation propagates rather than
/// being reported as a fault, so a host shutting down does not raise a false alarm.
/// </para>
/// </remarks>
internal sealed class EventStoreHealthCheck : IHealthCheck
{
	/// <summary>
	/// Sentinel aggregate ID used for probing. Reserved, intentionally non-existent, and never written.
	/// </summary>
	internal const string ProbeAggregateId = "__health_probe__";

	/// <summary>
	/// Sentinel aggregate type used for probing. Reserved, intentionally non-existent, and never written.
	/// </summary>
	internal const string ProbeAggregateType = "__health__";

	private readonly IEventStore _eventStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventStoreHealthCheck" /> class.
	/// </summary>
	/// <param name="eventStore">The event store to probe.</param>
	public EventStoreHealthCheck(IEventStore eventStore)
	{
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			// An aggregate load binds the provider's full event column list plus its tenant term, so it is
			// a strict superset of what the global-stream read binds. One probe, maximal coverage.
			var events = await _eventStore
				.LoadAsync(ProbeAggregateId, ProbeAggregateType, cancellationToken)
				.ConfigureAwait(false);

			return HealthCheckResult.Healthy(
				$"Event store served a read of its event table ({events.Count} row(s) returned for the "
				+ "reserved probe aggregate), so that table and the columns its read statement binds exist.");
		}
		catch (OperationCanceledException)
		{
			// A host shutting down is not an unhealthy store. Let the framework observe the cancellation
			// rather than converting a rolling deploy into a page.
			throw;
		}
		catch (Exception ex) when (ex is TenantRequiredException or TenantShardNotFoundException)
		{
			// Configuration, not health. A health check runs with no ambient tenant, so a tenant-routing
			// store cannot resolve a shard unless a default is configured. Reporting Unhealthy here would
			// mark a working store broken — and a permanently-red check gets removed from the readiness
			// probe, leaving nothing. Reporting Healthy would claim a verification that never happened.
			return HealthCheckResult.Degraded(
				"Event store could not be probed: no tenant or shard is resolvable on a health-check scope, "
				+ "which has no ambient tenant. This is a configuration state, not a store fault, and "
				+ $"nothing about the store's schema was verified: {ex.Message}",
				exception: ex);
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
