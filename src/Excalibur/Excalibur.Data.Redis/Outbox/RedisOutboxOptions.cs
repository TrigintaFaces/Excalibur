// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Redis.Outbox;

/// <summary>
/// Configuration options for the Redis outbox store.
/// </summary>
public sealed class RedisOutboxOptions
{
	/// <summary>
	/// Gets or sets the Redis connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = "localhost:6379";

	/// <summary>
	/// Gets or sets the Redis database ID to use.
	/// </summary>
	[Range(0, int.MaxValue)]
	public int DatabaseId { get; set; }

	/// <summary>
	/// Gets or sets the key prefix for outbox entries.
	/// </summary>
	/// <remarks>
	/// Keys are formatted as: {KeyPrefix}:msg:{messageId} for message data
	/// and {KeyPrefix}:idx:* for various indexes (priority, scheduled, failed).
	/// </remarks>
	[Required]
	public string KeyPrefix { get; set; } = "outbox";

	/// <summary>
	/// Gets or sets the default time to live for sent messages in seconds.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 7 days (604800 seconds).
	/// Only applies to messages in Sent status.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int SentMessageTtlSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the connection timeout in milliseconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int ConnectTimeoutMs { get; set; } = 5000;

	/// <summary>
	/// Gets or sets the sync operation timeout in milliseconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int SyncTimeoutMs { get; set; } = 5000;

	/// <summary>
	/// Gets or sets a value indicating whether to abort on connect failure.
	/// </summary>
	public bool AbortOnConnectFail { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the password for Redis authentication.
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(Resources.RedisOutboxOptions_ConnectionStringRequired);
		}

		if (string.IsNullOrWhiteSpace(KeyPrefix))
		{
			throw new InvalidOperationException(Resources.RedisOutboxOptions_KeyPrefixRequired);
		}
	}
}
