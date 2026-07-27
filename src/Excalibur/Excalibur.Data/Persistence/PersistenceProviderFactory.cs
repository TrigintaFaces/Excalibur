// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

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
		PersistenceProviderOptions options)
		where TProvider : class, IPersistenceProvider
	{
		ArgumentNullException.ThrowIfNull(options);

		var provider = ActivatorUtilities.CreateInstance<TProvider>(_serviceProvider);

		// Initialize provider with configuration
		if (provider is IConfigurableProvider configurable)
		{
			configurable.Configure(options);
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
	/// Creates a provider instance by resolving the configured provider from keyed dependency injection.
	/// </summary>
	/// <remarks>
	/// Each provider package registers its <see cref="IPersistenceProvider"/> implementation under a
	/// stable string key (e.g. <c>"sqlserver"</c>, <c>"inmemory"</c>) via
	/// <see cref="ServiceCollectionServiceExtensions"/> keyed registrations. This method resolves the
	/// implementation that matches the configured provider key rather than the single ambient
	/// <see cref="IPersistenceProvider"/>, so distinct configured providers resolve to distinct
	/// implementations. An unregistered key fails fast with an actionable message.
	/// </remarks>
	private IPersistenceProvider CreateProviderInstance(PersistenceProviderOptions options)
	{
		var providerKey = ResolveProviderKey(options);

		var provider = _serviceProvider.GetKeyedService<IPersistenceProvider>(providerKey)
			?? throw new InvalidOperationException(
				$"No persistence provider is registered for key '{providerKey}' " +
				$"(provider '{options.Name}', type {options.Type}). " +
				$"Register the provider before requesting it (e.g. call the provider package's " +
				$"Add…Persistence extension), or correct the configured provider type.");

		// Configure the provider if it supports configuration
		if (provider is IConfigurableProvider configurable)
		{
			configurable.Configure(options);
		}

		return provider;
	}

	/// <summary>
	/// Maps a configured provider to the keyed-DI key under which its implementation is registered.
	/// </summary>
	/// <remarks>
	/// The keys mirror the string keys used by each provider package's keyed registration. A
	/// <see cref="PersistenceProviderType.Custom"/> provider is resolved by its configured
	/// <see cref="PersistenceProviderOptions.Name"/>, allowing provider packages that are not part of
	/// the closed enum (e.g. MySQL, OpenSearch) to be registered and resolved by name.
	/// </remarks>
	private static string ResolveProviderKey(PersistenceProviderOptions options) => options.Type switch
	{
		PersistenceProviderType.SqlServer => "sqlserver",
		PersistenceProviderType.Postgres => "postgres",
		PersistenceProviderType.MongoDB => "mongodb",
		PersistenceProviderType.Elasticsearch => "elasticsearch",
		PersistenceProviderType.Redis => "redis",
		PersistenceProviderType.InMemory => "inmemory",
		PersistenceProviderType.Custom => options.Name,
		_ => throw new InvalidOperationException(
			$"Unknown persistence provider type '{options.Type}' for provider '{options.Name}'."),
	};
}
