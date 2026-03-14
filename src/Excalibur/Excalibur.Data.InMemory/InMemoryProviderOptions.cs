// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Configuration options for in-memory provider.
/// </summary>
public sealed class InMemoryProviderOptions : IPersistenceOptions, IPersistenceResilienceOptions, IPersistencePoolingOptions, IPersistenceObservabilityOptions
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
	public string ConnectionString { get; set; } = "InMemory";

	/// <summary>
	/// Gets or sets provider-specific options.
	/// </summary>
	/// <value>
	/// Provider-specific options.
	/// </value>
	public IDictionary<string, object> ProviderSpecificOptions { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the maximum number of items per collection.
	/// </summary>
	/// <value>
	/// The maximum number of items per collection.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxItemsPerCollection { get; set; } = 10000;

	/// <summary>
	/// Gets or sets a value indicating whether this is a read-only provider.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this is a read-only provider; otherwise, <c>false</c>.
	/// </value>
	public bool IsReadOnly { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether detailed logging is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if detailed logging is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableDetailedLogging { get; set; }

	/// <summary>
	/// Gets or sets the pooling and timeout options.
	/// </summary>
	public InMemoryPoolingOptions Pool { get; set; } = new();

	/// <summary>
	/// Gets or sets the storage and resilience options.
	/// </summary>
	public InMemoryStorageOptions Storage { get; set; } = new();

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds. Delegates to <see cref="Pool"/>.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ConnectionTimeout
	{
		get => Pool.ConnectionTimeout;
		set => Pool.ConnectionTimeout = value;
	}

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds. Delegates to <see cref="Pool"/>.
	/// </value>
	[Range(1, int.MaxValue)]
	public int CommandTimeout
	{
		get => Pool.CommandTimeout;
		set => Pool.CommandTimeout = value;
	}

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	/// <value>
	/// The maximum retry attempts. Delegates to <see cref="Storage"/>.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts
	{
		get => Storage.MaxRetryAttempts;
		set => Storage.MaxRetryAttempts = value;
	}

	/// <summary>
	/// Gets or sets the retry delay in milliseconds.
	/// </summary>
	/// <value>
	/// The retry delay in milliseconds. Delegates to <see cref="Storage"/>.
	/// </value>
	[Range(0, int.MaxValue)]
	public int RetryDelayMilliseconds
	{
		get => Storage.RetryDelayMilliseconds;
		set => Storage.RetryDelayMilliseconds = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether connection pooling is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if connection pooling is enabled; otherwise, <c>false</c>. Delegates to <see cref="Pool"/>.
	/// </value>
	public bool EnableConnectionPooling
	{
		get => Pool.EnableConnectionPooling;
		set => Pool.EnableConnectionPooling = value;
	}

	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value>
	/// The maximum pool size. Delegates to <see cref="Pool"/>.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize
	{
		get => Pool.MaxPoolSize;
		set => Pool.MaxPoolSize = value;
	}

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value>
	/// The minimum pool size. Delegates to <see cref="Pool"/>.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize
	{
		get => Pool.MinPoolSize;
		set => Pool.MinPoolSize = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether metrics are enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if metrics are enabled; otherwise, <c>false</c>. Delegates to <see cref="Storage"/>.
	/// </value>
	public bool EnableMetrics
	{
		get => Storage.EnableMetrics;
		set => Storage.EnableMetrics = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether to persist data to disk on dispose.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if data is persisted to disk on dispose; otherwise, <c>false</c>. Delegates to <see cref="Storage"/>.
	/// </value>
	public bool PersistToDisk
	{
		get => Storage.PersistToDisk;
		set => Storage.PersistToDisk = value;
	}

	/// <summary>
	/// Gets or sets the file path for persistence.
	/// </summary>
	/// <value>
	/// The file path for persistence. Delegates to <see cref="Storage"/>.
	/// </value>
	public string? PersistenceFilePath
	{
		get => Storage.PersistenceFilePath;
		set => Storage.PersistenceFilePath = value;
	}

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (MaxItemsPerCollection <= 0)
		{
			throw new ArgumentException("MaxItemsPerCollection must be greater than 0");
		}

		Storage.Validate();
	}
}
