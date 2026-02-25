// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines the failover strategy when automatic failover is triggered.
/// </summary>
public enum FailoverStrategy
{
	/// <summary>
	/// Fail over immediately when the threshold is met.
	/// Fastest RTO but may cause split-brain if network partition heals quickly.
	/// </summary>
	Immediate,

	/// <summary>
	/// Wait for a grace period after threshold is met before failing over.
	/// Reduces false positives from transient issues.
	/// </summary>
	GracePeriod,

	/// <summary>
	/// Require quorum confirmation (when using more than 2 regions).
	/// Most resilient to split-brain but requires 3+ regions.
	/// </summary>
	Quorum
}

/// <summary>
/// Configuration options for multi-region key management.
/// </summary>
/// <remarks>
/// <para>
/// Multi-region configuration uses active-passive architecture
/// with automatic failover and manual failback.
/// </para>
/// <para>
/// <strong>RPO/RTO Targets:</strong>
/// <list type="bullet">
///   <item><description>RPO (Recovery Point Objective): 15 minutes default</description></item>
///   <item><description>RTO (Recovery Time Objective): 5 minutes default</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MultiRegionOptions
{
	/// <summary>
	/// Gets or sets the primary region configuration.
	/// </summary>
	public required RegionConfiguration Primary { get; set; }

	/// <summary>
	/// Gets or sets the secondary region configuration.
	/// </summary>
	public required RegionConfiguration Secondary { get; set; }

	/// <summary>
	/// Gets or sets the replication mode between regions.
	/// </summary>
	/// <value> Defaults to <see cref="ReplicationMode.Asynchronous"/>. </value>
	public ReplicationMode ReplicationMode { get; set; } = ReplicationMode.Asynchronous;

	/// <summary>
	/// Gets or sets the target RPO (Recovery Point Objective).
	/// </summary>
	/// <value> Defaults to 15 minutes. </value>
	public TimeSpan RpoTarget { get; set; } = TimeSpan.FromMinutes(15);

	/// <summary>
	/// Gets or sets the target RTO (Recovery Time Objective).
	/// </summary>
	/// <value> Defaults to 5 minutes. </value>
	public TimeSpan RtoTarget { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the timeout for individual region operations.
	/// </summary>
	/// <value> Defaults to 10 seconds. </value>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets a value indicating whether to emit OpenTelemetry metrics.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to emit audit events.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool EnableAuditEvents { get; set; } = true;

	/// <summary>
	/// Gets or sets health check and failover configuration.
	/// </summary>
	/// <value> The failover sub-options. </value>
	public MultiRegionFailoverOptions Failover { get; set; } = new();

	// --- Backward-compatible shims ---

	/// <summary>
	/// Gets or sets the interval between health checks.
	/// </summary>
	/// <value> Defaults to 30 seconds. </value>
	public TimeSpan HealthCheckInterval { get => Failover.HealthCheckInterval; set => Failover.HealthCheckInterval = value; }

	/// <summary>
	/// Gets or sets the number of consecutive health check failures required to trigger automatic failover.
	/// </summary>
	/// <value> Defaults to 3. </value>
	public int FailoverThreshold { get => Failover.FailoverThreshold; set => Failover.FailoverThreshold = value; }

	/// <summary>
	/// Gets or sets a value indicating whether automatic failover is enabled.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool EnableAutomaticFailover { get => Failover.EnableAutomaticFailover; set => Failover.EnableAutomaticFailover = value; }

	/// <summary>
	/// Gets or sets the interval between replication syncs for asynchronous mode.
	/// </summary>
	/// <value> Defaults to 5 minutes. </value>
	public TimeSpan AsyncReplicationInterval { get => Failover.AsyncReplicationInterval; set => Failover.AsyncReplicationInterval = value; }
}

/// <summary>
/// Health check and failover configuration for multi-region key management.
/// </summary>
public sealed class MultiRegionFailoverOptions
{
	/// <summary>
	/// Gets or sets the interval between health checks.
	/// </summary>
	/// <value> Defaults to 30 seconds. </value>
	public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the number of consecutive health check failures required to trigger automatic failover.
	/// </summary>
	/// <value> Defaults to 3. </value>
	public int FailoverThreshold { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether automatic failover is enabled.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool EnableAutomaticFailover { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between replication syncs for asynchronous mode.
	/// </summary>
	/// <value> Defaults to 5 minutes. </value>
	public TimeSpan AsyncReplicationInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Configuration for a single region in multi-region key management.
/// </summary>
public sealed class RegionConfiguration
{
	/// <summary>
	/// Gets or sets the region identifier.
	/// </summary>
	public required string RegionId { get; set; }

	/// <summary>
	/// Gets or sets the display name for the region.
	/// </summary>
	public string? DisplayName { get; set; }

	/// <summary>
	/// Gets or sets the endpoint URI for the key management service in this region.
	/// </summary>
	public required Uri Endpoint { get; set; }

	/// <summary>
	/// Gets or sets provider-specific configuration.
	/// </summary>
	public IReadOnlyDictionary<string, string>? ProviderConfiguration { get; set; }

	/// <summary>
	/// Gets or sets the priority for this region (lower = higher priority).
	/// </summary>
	/// <value> Defaults to 0 for primary, 1 for secondary. </value>
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets the maximum acceptable latency for this region.
	/// </summary>
	/// <value> Defaults to 500ms. </value>
	public TimeSpan MaxAcceptableLatency { get; set; } = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Gets or sets a value indicating whether this region is enabled.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool Enabled { get; set; } = true;
}

/// <summary>
/// Extended options for multi-region failover behavior.
/// </summary>
public sealed class FailoverOptions
{
	/// <summary>
	/// Gets or sets the failover strategy.
	/// </summary>
	/// <value> Defaults to <see cref="FailoverStrategy.GracePeriod"/>. </value>
	public FailoverStrategy Strategy { get; set; } = FailoverStrategy.GracePeriod;

	/// <summary>
	/// Gets or sets the grace period before failover when using <see cref="FailoverStrategy.GracePeriod"/>.
	/// </summary>
	/// <value> Defaults to 30 seconds. </value>
	public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to notify on failover events.
	/// </summary>
	/// <value> Defaults to <c>true</c>. </value>
	public bool EnableNotifications { get; set; } = true;

	/// <summary>
	/// Gets or sets the cooldown period after a failover before another failover can occur.
	/// </summary>
	/// <value> Defaults to 5 minutes. </value>
	public TimeSpan FailoverCooldown { get; set; } = TimeSpan.FromMinutes(5);
}
