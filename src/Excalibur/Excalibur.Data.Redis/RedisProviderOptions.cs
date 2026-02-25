// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Redis;

/// <summary>
/// Configuration options for Redis provider.
/// </summary>
public sealed class RedisProviderOptions
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
	/// Gets or sets the password (if not included in connection string).
	/// </summary>
	/// <value>
	/// The password.
	/// </value>
	public string? Password { get; set; }

	/// <summary>
	/// Gets or sets the database ID to use.
	/// </summary>
	/// <value>
	/// The database ID to use.
	/// </value>
	[Range(0, int.MaxValue)]
	public int DatabaseId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if SSL/TLS is used; otherwise, <c>false</c>.
	/// </value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to allow admin operations.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if admin operations are allowed; otherwise, <c>false</c>.
	/// </value>
	public bool AllowAdmin { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a read-only provider.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this is a read-only provider; otherwise, <c>false</c>.
	/// </value>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// Gets or sets connection pool and timeout options.
	/// </summary>
	public RedisConnectionPoolOptions Pool { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	public int ConnectTimeout { get => Pool.ConnectTimeout; set => Pool.ConnectTimeout = value; }

	/// <summary>
	/// Gets or sets the synchronous operation timeout in seconds.
	/// </summary>
	/// <value>
	/// The synchronous operation timeout in seconds.
	/// </value>
	public int SyncTimeout { get => Pool.SyncTimeout; set => Pool.SyncTimeout = value; }

	/// <summary>
	/// Gets or sets the asynchronous operation timeout in seconds.
	/// </summary>
	/// <value>
	/// The asynchronous operation timeout in seconds.
	/// </value>
	public int AsyncTimeout { get => Pool.AsyncTimeout; set => Pool.AsyncTimeout = value; }

	/// <summary>
	/// Gets or sets the number of connection retry attempts.
	/// </summary>
	/// <value>
	/// The number of connection retry attempts.
	/// </value>
	public int ConnectRetry { get => Pool.ConnectRetry; set => Pool.ConnectRetry = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to abort on connect failure.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the connection aborts on failure; otherwise, <c>false</c>.
	/// </value>
	public bool AbortOnConnectFail { get => Pool.AbortOnConnectFail; set => Pool.AbortOnConnectFail = value; }

	/// <summary>
	/// Gets or sets the retry count for operations.
	/// </summary>
	/// <value>
	/// The retry count for operations.
	/// </value>
	public int RetryCount { get => Pool.RetryCount; set => Pool.RetryCount = value; }
}

/// <summary>
/// Connection pool and timeout options for Redis.
/// </summary>
public sealed class RedisConnectionPoolOptions
{
	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds. Defaults to 10.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeout { get; set; } = 10;

	/// <summary>
	/// Gets or sets the synchronous operation timeout in seconds.
	/// </summary>
	/// <value>
	/// The synchronous operation timeout in seconds. Defaults to 5.
	/// </value>
	[Range(1, int.MaxValue)]
	public int SyncTimeout { get; set; } = 5;

	/// <summary>
	/// Gets or sets the asynchronous operation timeout in seconds.
	/// </summary>
	/// <value>
	/// The asynchronous operation timeout in seconds. Defaults to 5.
	/// </value>
	[Range(1, int.MaxValue)]
	public int AsyncTimeout { get; set; } = 5;

	/// <summary>
	/// Gets or sets the number of connection retry attempts.
	/// </summary>
	/// <value>
	/// The number of connection retry attempts. Defaults to 3.
	/// </value>
	[Range(0, int.MaxValue)]
	public int ConnectRetry { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether to abort on connect failure.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the connection aborts on failure; otherwise, <see langword="false"/>.
	/// </value>
	public bool AbortOnConnectFail { get; set; }

	/// <summary>
	/// Gets or sets the retry count for operations.
	/// </summary>
	/// <value>
	/// The retry count for operations. Defaults to 3.
	/// </value>
	[Range(0, int.MaxValue)]
	public int RetryCount { get; set; } = 3;
}
