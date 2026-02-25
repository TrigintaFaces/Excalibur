// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.ErrorHandling;

/// <summary>
/// Configuration options for Postgres dead letter store.
/// </summary>
public sealed class PostgresDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	/// <value>The connection string for Postgres.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database schema for dead letter tables.
	/// </summary>
	/// <value>The schema name. Defaults to "public" (Postgres default schema).</value>
	[Required]
	public string SchemaName { get; set; } = "public";

	/// <summary>
	/// Gets or sets the table name for dead letter messages.
	/// </summary>
	/// <value>The table name. Defaults to "dead_letter_messages".</value>
	[Required]
	public string TableName { get; set; } = "dead_letter_messages";
}
