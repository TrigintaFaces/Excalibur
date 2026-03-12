// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Connection pooling options for SQL Server provider.
/// </summary>
/// <remarks>
/// Follows the <c>SqlConnectionStringBuilder</c> separation pattern
/// where pooling properties are grouped separately from connection settings.
/// </remarks>
public sealed class SqlServerPoolingOptions
{
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
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value> <c> true </c> if the connection pool is cleared on dispose; otherwise, <c> false </c>. </value>
	public bool ClearPoolOnDispose { get; set; }
}
