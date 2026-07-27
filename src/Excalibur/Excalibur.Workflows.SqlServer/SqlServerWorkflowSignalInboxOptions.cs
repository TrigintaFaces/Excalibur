// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Workflows.SqlServer;

/// <summary>
/// Configuration options for the SQL Server durable workflow signal inbox.
/// </summary>
public sealed class SqlServerWorkflowSignalInboxOptions
{
    /// <summary>
    /// Gets or sets the SQL Server connection string.
    /// </summary>
    /// <value>The connection string used to reach the durable signal-inbox table.</value>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema name for the signal-inbox table.
    /// </summary>
    /// <value>The database schema name. Defaults to <c>dbo</c>.</value>
    public string SchemaName { get; set; } = "dbo";

    /// <summary>
    /// Gets or sets the table name for admitted workflow signals.
    /// </summary>
    /// <value>The table name. Defaults to <c>workflow_signal_inbox</c>.</value>
    public string TableName { get; set; } = "workflow_signal_inbox";

    /// <summary>
    /// Gets or sets the command timeout in seconds.
    /// </summary>
    /// <value>The timeout duration. Defaults to 30 seconds.</value>
    [Range(1, int.MaxValue)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets the fully qualified table name including schema.
    /// </summary>
    /// <value>The qualified table name in the form <c>[schema].[table]</c>.</value>
    public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";
}
