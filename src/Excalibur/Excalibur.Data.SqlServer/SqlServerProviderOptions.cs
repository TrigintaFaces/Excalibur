// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Configuration options for SQL Server provider.
/// </summary>
/// <remarks>
/// <para>
/// Connection properties are in <see cref="Connection"/> and pooling properties are in <see cref="Pooling"/>.
/// This follows the <c>SqlConnectionStringBuilder</c> pattern of separating connection from pooling configuration.
/// </para>
/// </remarks>
public sealed class SqlServerProviderOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value> The provider name. </value>
	public string? Name { get; set; }

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
	/// Gets or sets the connection options.
	/// </summary>
	/// <value> The SQL Server connection options. </value>
	public SqlServerConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Gets or sets the pooling options.
	/// </summary>
	/// <value> The SQL Server pooling options. </value>
	public SqlServerPoolingOptions Pooling { get; set; } = new();
}
