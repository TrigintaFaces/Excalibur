// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Configuration options for the Postgres health-based leader election.
/// </summary>
public sealed class PostgresHealthBasedLeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the schema name for the health tracking table.
	/// </summary>
	/// <value>Defaults to <c>"public"</c>.</value>
	[Required]
	public string SchemaName { get; set; } = "public";

	/// <summary>
	/// Gets or sets the table name for candidate health tracking.
	/// </summary>
	/// <value>Defaults to <c>"leader_election_health"</c>.</value>
	[Required]
	public string TableName { get; set; } = "leader_election_health";

	/// <summary>
	/// Gets or sets a value indicating whether to automatically create the health table.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool AutoCreateTable { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the leader should step down when unhealthy.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool StepDownWhenUnhealthy { get; set; } = true;

	/// <summary>
	/// Gets or sets the health data expiration in seconds.
	/// </summary>
	/// <remarks>
	/// Candidate health records older than this value are considered stale.
	/// </remarks>
	/// <value>Defaults to <c>60</c> seconds.</value>
	[Range(5, 3600)]
	public int HealthExpirationSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>Defaults to <c>5</c> seconds.</value>
	[Range(1, 300)]
	public int CommandTimeoutSeconds { get; set; } = 5;
}
