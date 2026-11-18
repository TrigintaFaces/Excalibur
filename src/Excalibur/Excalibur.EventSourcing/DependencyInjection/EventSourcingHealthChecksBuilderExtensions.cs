// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Provides extension methods to register event sourcing health checks.</summary>
public static class EventSourcingHealthChecksBuilderExtensions
{
	private static readonly string[] DefaultTags = ["excalibur", "eventsourcing"];

	/// <summary>Adds the event store health check.</summary>
	/// <remarks>
	/// <para>
	/// The check reads the global event stream, so the composition MUST register a global-stream reader.
	/// The relational and in-memory event-store providers register one; document-store providers do not,
	/// and a composition without one is REFUSED AT STARTUP rather than handed a check that would throw
	/// when the health endpoint is first scraped.
	/// </para>
	/// <para>
	/// It establishes that the event table and the columns its read statement binds exist, on an empty
	/// database. It does not establish the write path, row materialization, any other schema object, or
	/// any tenant shard's schema — on a sharded deployment it verifies the primary store only.
	/// </para>
	/// </remarks>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddEventStoreHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "event-store",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;

		// Refuse a composition with no global-stream reader AT STARTUP rather than when the health
		// endpoint is first scraped. Without this the alternatives are both dishonest: a check that
		// probes something else makes a green mean different things on different compositions, and a
		// check that throws reports a registration mistake as an unhealthy application.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, EventStoreHealthCheckPrerequisiteValidator>());
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, EventStoreHealthCheckPrerequisiteValidator>());

		return builder.Add(new HealthCheckRegistration(
			name,
			// Resolved explicitly rather than through ActivatorUtilities so the dependency this check
			// needs is visible at the registration site instead of inferred from a constructor.
			sp => new EventStoreHealthCheck(sp.GetRequiredService<IGlobalStreamQuery>()),
			failureStatus,
			tags));
	}

	/// <summary>Adds the snapshot store health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddSnapshotStoreHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "snapshot-store",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<SnapshotStoreHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds the tenant shard health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddTenantShardHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "tenant-shard",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		return builder.AddTenantShardHealthCheck(new TenantShardHealthCheckOptions(), name, failureStatus, tags);
	}


	/// <summary>Adds physical-shard readiness reads with explicit concurrency and timeout settings.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="options">Probe limits, copied at registration rather than retained as mutable settings.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder.</returns>
	public static IHealthChecksBuilder AddTenantShardHealthCheck(
		this IHealthChecksBuilder builder, TenantShardHealthCheckOptions options,
		string name = "tenant-shard", HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		options.Validate();
		var concurrency = options.MaxConcurrentProbes;
		var timeout = options.Timeout;
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(name,
			sp => new TenantShardHealthCheck(sp.GetRequiredService<ITenantShardMap>(),
				sp.GetService<ITenantStoreResolver<IEventStore>>(), concurrency),
			failureStatus, tags, timeout));
	}

	/// <summary>Adds the projections health check.</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddProjectionsHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "projections",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		builder.Services.TryAddSingleton<ProjectionHealthState>();
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<ProjectionHealthCheck>(sp),
			failureStatus,
			tags));
	}

	/// <summary>Adds all event sourcing health checks (event store, snapshot store, tenant shard, projections).</summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="failureStatus">Optional failure status.</param>
	/// <param name="tags">Optional tags for filtering.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddEventSourcingHealthChecks(
		this IHealthChecksBuilder builder,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder
			.AddEventStoreHealthCheck(failureStatus: failureStatus, tags: tags)
			.AddSnapshotStoreHealthCheck(failureStatus: failureStatus, tags: tags)
			.AddTenantShardHealthCheck(failureStatus: failureStatus, tags: tags)
			.AddProjectionsHealthCheck(failureStatus: failureStatus, tags: tags);
	}
}
