// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.LeaderElection.Health;

/// <summary>
/// ASP.NET Core health check for leader election infrastructure monitoring.
/// </summary>
/// <remarks>
/// <para>
/// This health check evaluates leader election health based on:
/// <list type="bullet">
/// <item><description><b>Healthy</b>: This instance is the leader, or a valid leader is observed (<see cref="ILeaderElection.CurrentLeaderId"/> is not <see langword="null"/>)</description></item>
/// <item><description><b>Degraded</b>: No leader is detected (<see cref="ILeaderElection.CurrentLeaderId"/> is <see langword="null"/>), but the service is running</description></item>
/// <item><description><b>Unhealthy</b>: An exception occurs when querying leader election state</description></item>
/// </list>
/// </para>
/// <para>
/// The health check resolves <see cref="ILeaderElection"/> from DI and is provider-agnostic —
/// it works with SQL Server, Redis, Consul, Kubernetes, or any other <see cref="ILeaderElection"/> implementation.
/// </para>
/// </remarks>
public sealed class LeaderElectionHealthCheck : IHealthCheck
{
	private readonly ILeaderElection _leaderElection;

	/// <summary>
	/// Initializes a new instance of the <see cref="LeaderElectionHealthCheck"/> class.
	/// </summary>
	/// <param name="leaderElection">The leader election service.</param>
	public LeaderElectionHealthCheck(ILeaderElection leaderElection)
	{
		_leaderElection = leaderElection ?? throw new ArgumentNullException(nameof(leaderElection));
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var data = new Dictionary<string, object>
			{
				["IsLeader"] = _leaderElection.IsLeader,
				["CurrentLeaderId"] = _leaderElection.CurrentLeaderId ?? "(none)",
				["CandidateId"] = _leaderElection.CandidateId,
			};

			// Healthy: this instance is leader or a valid leader is observed
			if (_leaderElection.IsLeader || _leaderElection.CurrentLeaderId is not null)
			{
				return Task.FromResult(HealthCheckResult.Healthy(
					_leaderElection.IsLeader
						? $"This instance ({_leaderElection.CandidateId}) is the leader"
						: $"Leader is {_leaderElection.CurrentLeaderId}",
					data: data));
			}

			// Degraded: no leader detected, but service is running
			return Task.FromResult(HealthCheckResult.Degraded(
				"No leader detected",
				data: data));
		}
		catch (Exception ex)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Leader election health check failed",
				exception: ex));
		}
	}
}
