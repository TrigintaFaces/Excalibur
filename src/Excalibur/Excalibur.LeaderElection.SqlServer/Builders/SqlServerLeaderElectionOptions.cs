// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// Options for configuring SQL Server leader election.
/// </summary>
public class SqlServerLeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	/// <value>The connection string, or <see langword="null"/> if not configured via connection string.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the lock resource name used for SQL Server application locks.
	/// </summary>
	/// <value>The lock resource name (e.g., "MyApp.Leader").</value>
	public string? LockResource { get; set; }
}
