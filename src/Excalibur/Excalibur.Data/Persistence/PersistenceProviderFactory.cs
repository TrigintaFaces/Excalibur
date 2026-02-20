// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Implementation of the persistence provider factory.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PersistenceProviderFactory" /> class. </remarks>
internal sealed partial class PersistenceProviderFactory(
	IPersistenceConfiguration configuration,
	IServiceProvider serviceProvider,
	ILogger<PersistenceProviderFactory> logger) : IPersistenceProviderFactory, IAsyncDisposable
{
	private readonly PersistenceConfiguration _configuration = (configuration as PersistenceConfiguration) ??
															   throw new ArgumentException(
																   "Configuration must be of type PersistenceConfiguration",
																   nameof(configuration));

	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ILogger<PersistenceProviderFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ConcurrentDictionary<string, IPersistenceProvider> _providers = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _providerLock = new(1, 1);

	/// <inheritdoc />
	public IPersistenceProvider GetProvider()
	{
		if (string.IsNullOrWhiteSpace(_configuration.DefaultProvider))
		{
			throw new InvalidOperationException("No default provider configured.");
		}

		return GetProvider(_configuration.DefaultProvider);
	}

	/// <inheritdoc />
	public IPersistenceProvider GetProvider(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (_providers.TryGetValue(name, out var provider))
		{
			return provider;
		}

		// Try to create the provider Use timeout to prevent indefinite blocking and potential deadlocks
		if (!_providerLock.Wait(TimeSpan.FromSeconds(30)))
		{
			throw new InvalidOperationException($"Failed to acquire provider creation lock for '{name}' within timeout period.");
		}

		try
		{
			// Double-check after acquiring lock
			if (_providers.TryGetValue(name, out provider))
			{
				return provider;
			}

			if (!_configuration.Providers.TryGetValue(name, out var config))
			{
				throw new InvalidOperationException($"Provider '{name}' is not configured.");
			}

			provider = CreateProviderInstance(config);
			_providers[name] = provider;

			LogProviderCreated(_logger, name, config.Type);

			return provider;
		}
		finally
		{
			_ = _providerLock.Release();
		}
	}

	/// <inheritdoc />
	public bool TryGetProvider(string name, out IPersistenceProvider? provider)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		try
		{
			provider = GetProvider(name);
			return true;
		}
		catch
		{
			provider = null;
			return false;
		}
	}

	/// <inheritdoc />
	public IEnumerable<string> GetProviderNames() => _configuration.Providers.Keys;

	/// <inheritdoc />
	public TProvider CreateProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
		ProviderConfiguration configuration)
		where TProvider : class, IPersistenceProvider
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var provider = ActivatorUtilities.CreateInstance<TProvider>(_serviceProvider);

		// Initialize provider with configuration
		if (provider is IConfigurableProvider configurable)
		{
			configurable.Configure(configuration);
		}

		return provider;
	}

	/// <inheritdoc />
	public void RegisterProvider(string name, IPersistenceProvider provider)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(provider);

		if (!_providers.TryAdd(name, provider))
		{
			throw new InvalidOperationException($"Provider '{name}' is already registered.");
		}

		LogProviderRegistered(_logger, name);
	}

	/// <inheritdoc />
	public bool UnregisterProvider(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (_providers.TryRemove(name, out var provider))
		{
			provider?.Dispose();
			LogProviderUnregistered(_logger, name);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public TProvider CreateProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(string name)
		where TProvider : IPersistenceProvider
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (!_configuration.Providers.TryGetValue(name, out var config))
		{
			throw new InvalidOperationException($"Provider '{name}' is not configured.");
		}

		var provider = ActivatorUtilities.CreateInstance<TProvider>(_serviceProvider);

		// Initialize provider with configuration
		if (provider is IConfigurableProvider configurable)
		{
			configurable.Configure(config);
		}

		return provider;
	}

	/// <inheritdoc />
	public TProvider CreateProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
		where TProvider : IPersistenceProvider
	{
		if (string.IsNullOrWhiteSpace(_configuration.DefaultProvider))
		{
			throw new InvalidOperationException("No default provider configured.");
		}

		return CreateProvider<TProvider>(_configuration.DefaultProvider);
	}

	/// <inheritdoc />
	public async Task DisposeAllProvidersAsync()
	{
		foreach (var provider in _providers.Values)
		{
			if (provider is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				provider?.Dispose();
			}
		}

		_providers.Clear();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		foreach (var provider in _providers.Values)
		{
			if (provider is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				provider?.Dispose();
			}
		}

		_providers.Clear();
		_providerLock?.Dispose();
	}

	[LoggerMessage(DataEventId.ProviderCreated, LogLevel.Information, "Created persistence provider '{ProviderName}' of type {ProviderType}")]
	private static partial void LogProviderCreated(ILogger logger, string providerName, PersistenceProviderType providerType);

	[LoggerMessage(DataEventId.ProviderRegistered, LogLevel.Information, "Registered persistence provider '{ProviderName}'")]
	private static partial void LogProviderRegistered(ILogger logger, string providerName);

	[LoggerMessage(DataEventId.ProviderUnregistered, LogLevel.Information, "Unregistered persistence provider '{ProviderName}'")]
	private static partial void LogProviderUnregistered(ILogger logger, string providerName);

	/// <summary>
	/// Creates a provider instance based on configuration.
	/// </summary>
	private IPersistenceProvider CreateProviderInstance(ProviderConfiguration config)
	{
		// Resolve provider type from service container based on configuration
		var providerType = config.Type switch
		{
			PersistenceProviderType.SqlServer => typeof(IPersistenceProvider), // Would be SqlServerProvider
			PersistenceProviderType.Postgres => typeof(IPersistenceProvider), // Would be PostgresProvider
			PersistenceProviderType.MongoDB => typeof(IPersistenceProvider), // Would be MongoDbProvider
			PersistenceProviderType.Elasticsearch => typeof(IPersistenceProvider), // Would be ElasticsearchProvider
			PersistenceProviderType.Redis => typeof(IPersistenceProvider), // Would be RedisProvider
			PersistenceProviderType.InMemory => typeof(IPersistenceProvider), // Would be InMemoryProvider
			_ => typeof(IPersistenceProvider),
		};

		var provider = (IPersistenceProvider)_serviceProvider.GetRequiredService(providerType);

		// Configure the provider if it supports configuration
		if (provider is IConfigurableProvider configurable)
		{
			configurable.Configure(config);
		}

		return provider;
	}
}
