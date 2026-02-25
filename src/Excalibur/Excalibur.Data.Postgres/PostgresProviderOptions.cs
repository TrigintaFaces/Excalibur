// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Npgsql;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Configuration options for Postgres provider.
/// </summary>
public sealed class PostgresProviderOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value> The provider name. </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value> The connection string. </value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value> The command timeout in seconds. </value>
	[Range(1, int.MaxValue)]
	public int CommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value> The connection timeout in seconds. </value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeout { get; set; } = 15;

	/// <summary>
	/// Gets or sets the application name for the connection.
	/// </summary>
	/// <value> The application name for the connection. </value>
	public string? ApplicationName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include error detail in exceptions.
	/// </summary>
	/// <value> <see langword="true"/> if error detail is included in exceptions; otherwise, <c>false</c>. </value>
	public bool IncludeErrorDetail { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use NpgsqlDataSource.
	/// </summary>
	/// <value> <see langword="true"/> if NpgsqlDataSource is used; otherwise, <c>false</c>. </value>
	public bool UseDataSource { get; set; } = true;

	/// <summary>
	/// Gets or sets the retry count for transient failures.
	/// </summary>
	/// <value> The retry count for transient failures. </value>
	[Range(0, int.MaxValue)]
	public int RetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets connection pool configuration.
	/// </summary>
	/// <value> The connection pool sub-options. </value>
	public PostgresPoolOptions Pool { get; set; } = new();

	/// <summary>
	/// Gets or sets advanced Postgres configuration (SSL, statement preparation, JSONB).
	/// </summary>
	/// <value> The advanced sub-options. </value>
	public PostgresAdvancedOptions Advanced { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets a value indicating whether to prepare statements.
	/// </summary>
	/// <value> <see langword="true"/> if statements are prepared; otherwise, <c>false</c>. </value>
	public bool PrepareStatements { get => Advanced.PrepareStatements; set => Advanced.PrepareStatements = value; }

	/// <summary>
	/// Gets or sets the maximum number of auto-prepared statements.
	/// </summary>
	/// <value> The maximum number of auto-prepared statements. </value>
	public int MaxAutoPrepare { get => Advanced.MaxAutoPrepare; set => Advanced.MaxAutoPrepare = value; }

	/// <summary>
	/// Gets or sets the minimum number of usages before auto-preparing.
	/// </summary>
	/// <value> The minimum number of usages before auto-preparing. </value>
	public int AutoPrepareMinUsages { get => Advanced.AutoPrepareMinUsages; set => Advanced.AutoPrepareMinUsages = value; }

	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value> The maximum pool size. </value>
	public int MaxPoolSize { get => Pool.MaxPoolSize; set => Pool.MaxPoolSize = value; }

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value> The minimum pool size. </value>
	public int MinPoolSize { get => Pool.MinPoolSize; set => Pool.MinPoolSize = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value> <see langword="true"/> if connection pooling is enabled; otherwise, <c>false</c>. </value>
	public bool EnablePooling { get => Pool.EnablePooling; set => Pool.EnablePooling = value; }

	/// <summary>
	/// Gets or sets the keep-alive interval in seconds.
	/// </summary>
	/// <value> The keep-alive interval in seconds. </value>
	public int KeepAlive { get => Advanced.KeepAlive; set => Advanced.KeepAlive = value; }

	/// <summary>
	/// Gets or sets the connection idle lifetime in seconds.
	/// </summary>
	/// <value> The connection idle lifetime in seconds. </value>
	public int ConnectionIdleLifetime { get => Pool.ConnectionIdleLifetime; set => Pool.ConnectionIdleLifetime = value; }

	/// <summary>
	/// Gets or sets the connection pruning interval in seconds.
	/// </summary>
	/// <value> The connection pruning interval in seconds. </value>
	public int ConnectionPruningInterval { get => Pool.ConnectionPruningInterval; set => Pool.ConnectionPruningInterval = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value> <see langword="true"/> if SSL/TLS is used; otherwise, <c>false</c>. </value>
	public bool UseSsl { get => Advanced.UseSsl; set => Advanced.UseSsl = value; }

	/// <summary>
	/// Gets or sets the SSL mode.
	/// </summary>
	/// <value> The SSL mode. </value>
	public SslMode SslMode { get => Advanced.SslMode; set => Advanced.SslMode = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable JSONB support.
	/// </summary>
	/// <value> <see langword="true"/> if JSONB support is enabled; otherwise, <c>false</c>. </value>
	public bool EnableJsonb { get => Advanced.EnableJsonb; set => Advanced.EnableJsonb = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to open connections immediately.
	/// </summary>
	/// <value> <see langword="true"/> if connections are opened immediately; otherwise, <c>false</c>. </value>
	public bool OpenConnectionImmediately { get => Pool.OpenConnectionImmediately; set => Pool.OpenConnectionImmediately = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value> <see langword="true"/> if the connection pool is cleared on dispose; otherwise, <c>false</c>. </value>
	public bool ClearPoolOnDispose { get => Pool.ClearPoolOnDispose; set => Pool.ClearPoolOnDispose = value; }
}

/// <summary>
/// Connection pool configuration for Postgres provider.
/// </summary>
public sealed class PostgresPoolOptions
{
	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value> The maximum pool size. </value>
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value> The minimum pool size. </value>
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value> <see langword="true"/> if connection pooling is enabled; otherwise, <c>false</c>. </value>
	public bool EnablePooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the connection idle lifetime in seconds.
	/// </summary>
	/// <value> The connection idle lifetime in seconds. </value>
	public int ConnectionIdleLifetime { get; set; } = 300;

	/// <summary>
	/// Gets or sets the connection pruning interval in seconds.
	/// </summary>
	/// <value> The connection pruning interval in seconds. </value>
	public int ConnectionPruningInterval { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to open connections immediately.
	/// </summary>
	/// <value> <see langword="true"/> if connections are opened immediately; otherwise, <c>false</c>. </value>
	public bool OpenConnectionImmediately { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value> <see langword="true"/> if the connection pool is cleared on dispose; otherwise, <c>false</c>. </value>
	public bool ClearPoolOnDispose { get; set; }
}

/// <summary>
/// Advanced Postgres configuration (SSL, statement preparation, JSONB).
/// </summary>
public sealed class PostgresAdvancedOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to prepare statements.
	/// </summary>
	/// <value> <see langword="true"/> if statements are prepared; otherwise, <c>false</c>. </value>
	public bool PrepareStatements { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of auto-prepared statements.
	/// </summary>
	/// <value> The maximum number of auto-prepared statements. </value>
	public int MaxAutoPrepare { get; set; } = 20;

	/// <summary>
	/// Gets or sets the minimum number of usages before auto-preparing.
	/// </summary>
	/// <value> The minimum number of usages before auto-preparing. </value>
	public int AutoPrepareMinUsages { get; set; } = 2;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value> <see langword="true"/> if SSL/TLS is used; otherwise, <c>false</c>. </value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the SSL mode.
	/// </summary>
	/// <value> The SSL mode. </value>
	public SslMode SslMode { get; set; } = SslMode.Prefer;

	/// <summary>
	/// Gets or sets the keep-alive interval in seconds.
	/// </summary>
	/// <value> The keep-alive interval in seconds. </value>
	public int KeepAlive { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable JSONB support.
	/// </summary>
	/// <value> <see langword="true"/> if JSONB support is enabled; otherwise, <c>false</c>. </value>
	public bool EnableJsonb { get; set; } = true;
}
