// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Postgres;

/// <summary>
/// Configuration options for the Postgres audit store.
/// </summary>
public sealed class PostgresAuditOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for audit tables. Default is "audit".
	/// </summary>
	[Required]
	public string SchemaName { get; set; } = "audit";

	/// <summary>
	/// Gets or sets the main audit events table name. Default is "audit_events".
	/// </summary>
	[Required]
	public string TableName { get; set; } = "audit_events";

	/// <summary>
	/// Gets or sets the batch size for bulk insert operations. Default is 1000.
	/// </summary>
	[Range(1, 100000)]
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the default retention period for audit events.
	/// Default is 7 years (SOC2 requirement).
	/// </summary>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7 * 365);

	/// <summary>
	/// Gets or sets the maximum number of events to delete per cleanup batch. Default is 10000.
	/// </summary>
	[Range(1, 1000000)]
	public int RetentionCleanupBatchSize { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the command timeout for SQL operations in seconds. Default is 30.
	/// </summary>
	[Range(1, 3600)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to compute SHA-256 hash for hash chain integrity.
	/// </summary>
	public bool EnableHashChain { get; set; } = true;

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string FullyQualifiedTableName => $"\"{SchemaName}\".\"{TableName}\"";
}
