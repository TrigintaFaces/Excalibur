// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Configuration options for SQL Server provider.
/// </summary>
public sealed class SqlServerProviderOptions
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
	/// Gets or sets a value indicating whether to enable Multiple Active Result Sets.
	/// </summary>
	/// <value> <c> true </c> if Multiple Active Result Sets (MARS) is enabled; otherwise, <c> false </c>. </value>
	public bool EnableMars { get; set; } = true;

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
	/// Gets or sets a value indicating whether to encrypt the connection.
	/// </summary>
	/// <value> <c> true </c> if the connection is encrypted; otherwise, <c> false </c>. </value>
	public bool Encrypt { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to trust the server certificate.
	/// </summary>
	/// <value> <c> true </c> if the server certificate is trusted; otherwise, <c> false </c>. </value>
	public bool TrustServerCertificate { get; set; }

	/// <summary>
	/// Gets or sets the application name for the connection.
	/// </summary>
	/// <value> The application name for the connection. </value>
	public string? ApplicationName { get; set; }

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value> The minimum pool size. </value>
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value> The maximum pool size. </value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value> <c> true </c> if connection pooling is enabled; otherwise, <c> false </c>. </value>
	public bool EnablePooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the load balance timeout in seconds.
	/// </summary>
	/// <value> The load balance timeout in seconds. </value>
	public int LoadBalanceTimeout { get; set; }

	/// <summary>
	/// Gets or sets the retry count for transient failures.
	/// </summary>
	/// <value> The retry count for transient failures. </value>
	[Range(0, int.MaxValue)]
	public int RetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether to open connections immediately.
	/// </summary>
	/// <value> <c> true </c> if connections are opened immediately; otherwise, <c> false </c>. </value>
	public bool OpenConnectionImmediately { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value> <c> true </c> if the connection pool is cleared on dispose; otherwise, <c> false </c>. </value>
	public bool ClearPoolOnDispose { get; set; }
}
