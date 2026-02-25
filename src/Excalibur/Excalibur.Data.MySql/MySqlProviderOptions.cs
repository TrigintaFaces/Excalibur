// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MySql;

/// <summary>
/// Configuration options for the MySQL/MariaDB persistence provider.
/// </summary>
public sealed class MySqlProviderOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value>The provider name.</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value>The connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>The command timeout in seconds.</value>
	[Range(1, int.MaxValue)]
	public int CommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>The connection timeout in seconds.</value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeout { get; set; } = 15;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	/// <value>The maximum number of retry attempts.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value>The maximum pool size.</value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value>The minimum pool size.</value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value><see langword="true"/> if connection pooling is enabled; otherwise, <see langword="false"/>.</value>
	public bool EnablePooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the application name for the connection.
	/// </summary>
	/// <value>The application name for the connection.</value>
	public string? ApplicationName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value><see langword="true"/> if SSL/TLS is used; otherwise, <see langword="false"/>.</value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value><see langword="true"/> if the connection pool is cleared on dispose; otherwise, <see langword="false"/>.</value>
	public bool ClearPoolOnDispose { get; set; }
}
