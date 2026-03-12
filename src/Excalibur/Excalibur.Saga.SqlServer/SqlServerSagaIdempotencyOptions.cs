// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Configuration options for SQL Server saga idempotency tracking.
/// </summary>
public sealed class SqlServerSagaIdempotencyOptions
{
	/// <summary>
	/// Gets or sets the schema name for the idempotency table.
	/// </summary>
	/// <value>The schema name. Defaults to "dispatch".</value>
	[Required]
	public string SchemaName { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets the table name for idempotency entries.
	/// </summary>
	/// <value>The table name. Defaults to "saga_idempotency".</value>
	[Required]
	public string TableName { get; set; } = "saga_idempotency";

	/// <summary>
	/// Gets or sets the retention period for processed idempotency keys.
	/// Keys older than this period may be cleaned up.
	/// </summary>
	/// <value>The retention period. Defaults to 7 days.</value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets the fully qualified table name.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
