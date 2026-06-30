// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Provides extension methods to register event sourcing health checks.</summary>
public static class EventSourcingHealthChecksBuilderExtensions
{
	private static readonly string[] DefaultTags = ["excalibur", "eventsourcing"];

	/// <summary>Adds the event store health check.</summary>
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
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<EventStoreHealthCheck>(sp),
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
		ArgumentNullException.ThrowIfNull(builder);
		tags ??= DefaultTags;
		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<TenantShardHealthCheck>(sp),
			failureStatus,
			tags));
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
