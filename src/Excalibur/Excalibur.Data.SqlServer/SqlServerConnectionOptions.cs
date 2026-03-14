// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Connection-related options for SQL Server provider.
/// </summary>
/// <remarks>
/// Follows the <c>SqlConnectionStringBuilder</c> separation pattern
/// where connection properties are grouped separately from pooling and behavior settings.
/// </remarks>
public sealed class SqlServerConnectionOptions
{
	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value> The connection string. </value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

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
}
