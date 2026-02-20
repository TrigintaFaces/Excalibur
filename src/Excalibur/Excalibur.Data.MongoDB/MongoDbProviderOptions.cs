// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Configuration options for MongoDB provider.
/// </summary>
public sealed class MongoDbProviderOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value>
	/// The provider name.
	/// </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value>
	/// The connection string.
	/// </value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>
	/// The database name.
	/// </value>
	[Required]
	public string DatabaseName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// </summary>
	/// <value>
	/// The server selection timeout in seconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ServerSelectionTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if SSL/TLS is used; otherwise, <c>false</c>.
	/// </value>
	public bool UseSsl { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>
	/// The maximum connection pool size.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum connection pool size.
	/// </summary>
	/// <value>
	/// The minimum connection pool size.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use transactions.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if transactions are used; otherwise, <c>false</c>.
	/// </value>
	public bool UseTransactions { get; set; }

	/// <summary>
	/// Gets or sets the retry count for operations.
	/// </summary>
	/// <value>
	/// The retry count for operations.
	/// </value>
	[Range(0, int.MaxValue)]
	public int RetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether this is a read-only provider.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this is a read-only provider; otherwise, <c>false</c>.
	/// </value>
	public bool IsReadOnly { get; set; }
}
