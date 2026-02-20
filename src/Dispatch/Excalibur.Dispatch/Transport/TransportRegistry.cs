// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Registry for managing transport adapters and their configurations.
/// </summary>
public sealed class TransportRegistry
{
	private readonly ConcurrentDictionary<string, TransportRegistration> _transports = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, TransportFactoryRegistration> _factories = new(StringComparer.Ordinal);
	private string? _defaultTransportName;

	/// <summary>
	/// Gets a value indicating whether a default transport has been configured.
	/// </summary>
	/// <value>True if a default transport is set; otherwise, false.</value>
	public bool HasDefaultTransport => _defaultTransportName is not null;

	/// <summary>
	/// Gets the name of the default transport.
	/// </summary>
	/// <value>The default transport name, or null if not set.</value>
	public string? DefaultTransportName => _defaultTransportName;

	/// <summary>
	/// Gets a value indicating whether there are pending factory registrations.
	/// </summary>
	/// <value>True if there are factory registrations that have not been initialized.</value>
	public bool HasPendingFactories => !_factories.IsEmpty;

	/// <summary>
	/// Registers a transport adapter.
	/// </summary>
	/// <param name="name"> The transport name. </param>
	/// <param name="adapter"> The transport adapter. </param>
	/// <param name="transportType"> The transport type. </param>
	/// <param name="options"> Optional transport options. </param>
	/// <exception cref="InvalidOperationException"></exception>
	public void RegisterTransport(string name, ITransportAdapter adapter, string transportType, Dictionary<string, object>? options = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(adapter);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportType);

		var registration = new TransportRegistration(adapter, transportType, options ?? []);

		if (!_transports.TryAdd(name, registration))
		{
			throw new InvalidOperationException($"A transport with name '{name}' is already registered");
		}
	}

	/// <summary>
	/// Gets a transport adapter by name.
	/// </summary>
	/// <param name="name"> The transport name. </param>
	/// <returns> The transport adapter, or null if not found. </returns>
	public ITransportAdapter? GetTransportAdapter(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _transports.TryGetValue(name, out var registration) ? registration.Adapter : null;
	}

	/// <summary>
	/// Gets transport registration by name.
	/// </summary>
	/// <param name="name"> The transport name. </param>
	/// <returns> The transport registration, or null if not found. </returns>
	public TransportRegistration? GetTransportRegistration(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _transports.GetValueOrDefault(name);
	}

	/// <summary>
	/// Gets all registered transport names, including pending factories.
	/// </summary>
	/// <returns> Collection of transport names (both initialized and pending). </returns>
	public IEnumerable<string> GetTransportNames()
	{
		// Yield initialized transports first
		foreach (var key in _transports.Keys)
		{
			yield return key;
		}

		// Then yield pending factories that aren't already initialized
		foreach (var key in _factories.Keys)
		{
			if (!_transports.ContainsKey(key))
			{
				yield return key;
			}
		}
	}

	/// <summary>
	/// Gets all registered transports.
	/// </summary>
	/// <returns> Dictionary of transport names and their registrations. </returns>
	public IReadOnlyDictionary<string, TransportRegistration> GetAllTransports() =>
		_transports.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);

	/// <summary>
	/// Removes a transport by name.
	/// </summary>
	/// <param name="name"> The transport name. </param>
	/// <returns> True if the transport was removed; otherwise false. </returns>
	public bool RemoveTransport(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _transports.TryRemove(name, out _);
	}

	/// <summary>
	/// Clears all registered transports.
	/// </summary>
	public void Clear()
	{
		_transports.Clear();
		_defaultTransportName = null;
	}

	/// <summary>
	/// Sets the default transport by name.
	/// </summary>
	/// <param name="name"> The name of the transport to set as default. </param>
	/// <exception cref="InvalidOperationException"> Thrown when the specified transport is not registered. </exception>
	/// <remarks>
	/// <para>
	/// The default transport is used when no specific transport is specified for an operation.
	/// This allows consumers to register multiple transports and designate one as the primary.
	/// </para>
	/// </remarks>
	public void SetDefaultTransport(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		// Check both initialized transports and pending factories
		if (!_transports.ContainsKey(name) && !_factories.ContainsKey(name))
		{
			throw new InvalidOperationException(
				$"Cannot set default transport to '{name}': transport is not registered. " +
				$"Available transports: {string.Join(", ", GetTransportNames())}");
		}

		_defaultTransportName = name;
	}

	/// <summary>
	/// Gets the default transport adapter.
	/// </summary>
	/// <returns> The default transport adapter, or null if no default is set. </returns>
	public ITransportAdapter? GetDefaultTransportAdapter()
	{
		return _defaultTransportName is not null ? GetTransportAdapter(_defaultTransportName) : null;
	}

	/// <summary>
	/// Gets the default transport registration.
	/// </summary>
	/// <returns> The default transport registration, or null if no default is set. </returns>
	public TransportRegistration? GetDefaultTransportRegistration()
	{
		return _defaultTransportName is not null ? GetTransportRegistration(_defaultTransportName) : null;
	}

	/// <summary>
	/// Registers a transport adapter factory for deferred creation.
	/// </summary>
	/// <param name="name"> The transport name. </param>
	/// <param name="transportType"> The transport type identifier. </param>
	/// <param name="factory"> Factory function to create the adapter at runtime. </param>
	/// <exception cref="InvalidOperationException"> Thrown when a transport or factory with the same name is already registered. </exception>
	/// <remarks>
	/// <para>
	/// Factory-registered transports are resolved during application startup when
	/// <see cref="InitializeFactories"/> is called. This enables simple API patterns
	/// where the transport configuration is specified at registration time but the
	/// actual adapter creation is deferred until the service provider is available.
	/// </para>
	/// </remarks>
	public void RegisterTransportFactory(
		string name,
		string transportType,
		Func<IServiceProvider, ITransportAdapter> factory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportType);
		ArgumentNullException.ThrowIfNull(factory);

		if (_transports.ContainsKey(name))
		{
			throw new InvalidOperationException($"A transport with name '{name}' is already registered");
		}

		var registration = new TransportFactoryRegistration(transportType, factory);

		if (!_factories.TryAdd(name, registration))
		{
			throw new InvalidOperationException($"A transport factory with name '{name}' is already registered");
		}
	}

	/// <summary>
	/// Gets all pending factory names.
	/// </summary>
	/// <returns> Collection of factory names that have not been initialized. </returns>
	public IEnumerable<string> GetPendingFactoryNames() => _factories.Keys;

	/// <summary>
	/// Initializes all pending transport factories using the provided service provider.
	/// </summary>
	/// <param name="serviceProvider"> The service provider for creating adapters. </param>
	/// <returns> The number of factories that were initialized. </returns>
	/// <remarks>
	/// <para>
	/// This method should be called during application startup, typically by the
	/// <see cref="TransportAdapterHostedService"/>, before starting transport adapters.
	/// Each factory is invoked once, and the resulting adapter is registered as a
	/// normal transport. The factory registration is then removed.
	/// </para>
	/// </remarks>
	public int InitializeFactories(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

		var count = 0;

		foreach (var name in _factories.Keys.ToList())
		{
			if (_factories.TryRemove(name, out var factoryReg))
			{
				var adapter = factoryReg.Factory(serviceProvider);
				RegisterTransport(name, adapter, factoryReg.TransportType);
				count++;
			}
		}

		return count;
	}
}

/// <summary>
/// Registration record for factory-based transport creation.
/// </summary>
/// <param name="TransportType"> The transport type identifier. </param>
/// <param name="Factory"> Factory function to create the adapter. </param>
public sealed record TransportFactoryRegistration(
	string TransportType,
	Func<IServiceProvider, ITransportAdapter> Factory);
