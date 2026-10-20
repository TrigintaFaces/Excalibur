// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.SqlServer.Scheduling;

/// <summary>Configures bounded database operations and renewable occurrence ownership.</summary>
public sealed class SqlServerScheduleStoreOptions
{
	/// <summary>Gets or sets the required, nonblank SQL Server connection string.</summary>
	public string ConnectionString { get; set; } = string.Empty;
	/// <summary>Gets or sets the positive duration granted by authoritative acquisition and renewal.</summary>
	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
	/// <summary>Gets or sets the command timeout in seconds. Zero (unbounded) is rejected.</summary>
	public int CommandTimeoutSeconds { get; set; } = 30;
	/// <summary>Gets or sets the SQL lock wait limit in milliseconds. Zero fails immediately on contention; negative (unbounded) is rejected.</summary>
	public int LockTimeoutMilliseconds { get; set; } = 30_000;
}
