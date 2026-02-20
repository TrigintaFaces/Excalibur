// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging.SqlServer;

/// <summary>
/// Configuration options for the SQL Server audit store.
/// </summary>
public sealed class SqlServerAuditOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for audit tables. Default is "audit".
	/// </summary>
	public string SchemaName { get; set; } = "audit";

	/// <summary>
	/// Gets or sets the main audit events table name. Default is "AuditEvents".
	/// </summary>
	public string TableName { get; set; } = "AuditEvents";

	/// <summary>
	/// Gets or sets the batch size for bulk insert operations. Default is 1000.
	/// </summary>
	public int BatchInsertSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the default retention period for audit events.
	/// Events older than this will be eligible for cleanup. Default is 7 years (SOC2 requirement).
	/// </summary>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7 * 365);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retention enforcement.
	/// </summary>
	public bool EnableRetentionEnforcement { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for retention cleanup operations. Default is 1 day.
	/// </summary>
	public TimeSpan RetentionCleanupInterval { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets the maximum number of events to delete per cleanup batch. Default is 10000.
	/// </summary>
	public int RetentionCleanupBatchSize { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the command timeout for SQL operations in seconds. Default is 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use table partitioning by month.
	/// </summary>
	/// <remarks>
	/// Partitioning improves query performance and simplifies retention management
	/// but requires SQL Server Enterprise Edition or Azure SQL Database.
	/// </remarks>
	public bool UsePartitioning { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to compute SHA-256 hash for hash chain integrity.
	/// </summary>
	public bool EnableHashChain { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed telemetry for operations.
	/// </summary>
	public bool EnableDetailedTelemetry { get; set; }

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string FullyQualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
