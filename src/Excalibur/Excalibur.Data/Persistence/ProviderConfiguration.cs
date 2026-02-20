// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Represents configuration for a specific persistence provider.
/// </summary>
public sealed class ProviderConfiguration : IPersistenceOptions, IPersistenceResilienceOptions, IPersistencePoolingOptions, IPersistenceObservabilityOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value>
	/// The provider name.
	/// </value>
	public required string Name { get; set; }

	/// <summary>
	/// Gets or sets the provider type.
	/// </summary>
	/// <value>
	/// The provider type.
	/// </value>
	public required PersistenceProviderType Type { get; set; }

	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value>
	/// The connection string.
	/// </value>
	public required string ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this provider is read-only.
	/// </summary>
	/// <value>
	/// A value indicating whether this provider is read-only.
	/// </value>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>
	/// The maximum connection pool size.
	/// </value>
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	public int ConnectionTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds.
	/// </value>
	public int CommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable connection pooling.
	/// </value>
	public bool EnableConnectionPooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum pool size when connection pooling is enabled.
	/// </summary>
	/// <value>
	/// The minimum pool size when connection pooling is enabled.
	/// </value>
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for transient failures.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts in milliseconds.
	/// </summary>
	/// <value>
	/// The delay between retry attempts in milliseconds.
	/// </value>
	public int RetryDelayMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed logging.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable detailed logging.
	/// </value>
	public bool EnableDetailedLogging { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable metrics collection.
	/// </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets provider-specific options as key-value pairs.
	/// </summary>
	/// <value>
	/// Provider-specific options as key-value pairs.
	/// </value>
	public IDictionary<string, object> ProviderSpecificOptions { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <inheritdoc />
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException($"Connection string is required for provider '{Name}'.");
		}

		if (MaxPoolSize <= 0)
		{
			throw new InvalidOperationException($"MaxPoolSize must be greater than 0 for provider '{Name}'.");
		}

		if (ConnectionTimeout <= 0)
		{
			throw new InvalidOperationException($"ConnectionTimeout must be greater than 0 for provider '{Name}'.");
		}

		if (CommandTimeout <= 0)
		{
			throw new InvalidOperationException($"CommandTimeout must be greater than 0 for provider '{Name}'.");
		}
	}
}
