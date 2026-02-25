// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides centralized management and registration of message buses, including both local and remote bus instances. This class maintains
/// separate collections for local and remote message buses and provides methods to retrieve, register, and enumerate them.
/// </summary>
public sealed class MessageBusProvider : IMessageBusProvider
{
	private readonly ConcurrentDictionary<string, Lazy<IMessageBus>> _buses =
			new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<string, Lazy<IMessageBus>> _remoteBuses =
			new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageBusProvider" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider. </param>
	/// <param name="registrations"> The message bus registrations. </param>
	public MessageBusProvider(
			IServiceProvider serviceProvider,
			IEnumerable<IMessageBusRegistration> registrations)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(registrations);

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var registration in registrations)
		{
			if (registration is null)
			{
				continue;
			}

			if (!seen.Add(registration.Name))
			{
				throw new InvalidOperationException(string.Format(
						registration.IsRemote
								? ErrorConstants.RemoteMessageBusAlreadyRegistered
								: ErrorConstants.MessageBusAlreadyRegistered,
						registration.Name));
			}

			var target = registration.IsRemote ? _remoteBuses : _buses;
			var lazy = CreateLazy(serviceProvider, registration.Name);

			if (!target.TryAdd(registration.Name, lazy))
			{
				throw new InvalidOperationException(string.Format(
						registration.IsRemote
								? ErrorConstants.RemoteMessageBusAlreadyRegistered
								: ErrorConstants.MessageBusAlreadyRegistered,
						registration.Name));
			}
		}
	}

	/// <summary>
	/// Retrieves a message bus by name from either local or remote bus collections.
	/// </summary>
	/// <param name="name"> The name of the message bus to retrieve. </param>
	/// <returns> The message bus instance if found, otherwise null. </returns>
	public IMessageBus? GetMessageBus(string name) =>
			GetValueOrDefault(_buses, name) ?? GetValueOrDefault(_remoteBuses, name);

	/// <summary>
	/// Retrieves a remote message bus by name from the remote bus collection only.
	/// </summary>
	/// <param name="name"> The name of the remote message bus to retrieve. </param>
	/// <returns> The remote message bus instance if found, otherwise null. </returns>
	public IMessageBus? GetRemoteMessageBus(string name) => GetValueOrDefault(_remoteBuses, name);

	/// <summary>
	/// Gets all registered message buses from both local and remote collections.
	/// </summary>
	/// <returns> An enumerable collection of all registered message bus instances. </returns>
	public IEnumerable<IMessageBus> GetAllMessageBuses() =>
			_buses.Values.Select(static lazy => lazy.Value)
					.Concat(_remoteBuses.Values.Select(static lazy => lazy.Value));

	/// <summary>
	/// Gets all registered remote message buses.
	/// </summary>
	/// <returns> An enumerable collection of all registered remote message bus instances. </returns>
	public IEnumerable<IMessageBus> GetAllRemoteMessageBuses() =>
			_remoteBuses.Values.Select(static lazy => lazy.Value);

	/// <summary>
	/// Gets the names of all registered message buses from both local and remote collections.
	/// </summary>
	/// <returns> An enumerable collection of all registered message bus names. </returns>
	public IEnumerable<string> GetAllMessageBusNames() => _buses.Keys.Concat(_remoteBuses.Keys);

	/// <summary>
	/// Gets the names of all registered remote message buses.
	/// </summary>
	/// <returns> An enumerable collection of all registered remote message bus names. </returns>
	public IEnumerable<string> GetAllRemoteMessageBusNames() => _remoteBuses.Keys;

	/// <summary>
	/// Attempts to retrieve a message bus by name from either local or remote collections.
	/// </summary>
	/// <param name="name"> The name of the message bus to retrieve. </param>
	/// <param name="bus"> When this method returns, contains the message bus instance if found, or null if not found. </param>
	/// <returns> true if the message bus was found; otherwise, false. </returns>
	public bool TryGet(string name, out IMessageBus? bus)
	{
		if (_buses.TryGetValue(name, out var localBus))
		{
			bus = localBus.Value;
			return true;
		}

		if (_remoteBuses.TryGetValue(name, out var remoteBus))
		{
			bus = remoteBus.Value;
			return true;
		}

		bus = null;
		return false;
	}

	/// <summary>
	/// Attempts to retrieve a remote message bus by name from the remote collection.
	/// </summary>
	/// <param name="name"> The name of the remote message bus to retrieve. </param>
	/// <param name="bus"> When this method returns, contains the remote message bus instance if found, or null if not found. </param>
	/// <returns> true if the remote message bus was found; otherwise, false. </returns>
	public bool TryGetRemote(string name, out IMessageBus? bus)
	{
		if (_remoteBuses.TryGetValue(name, out var remoteBus))
		{
			bus = remoteBus.Value;
			return true;
		}

		bus = null;
		return false;
	}

	private static IMessageBus? GetValueOrDefault(
			ConcurrentDictionary<string, Lazy<IMessageBus>> source,
			string name)
	{
		return source.TryGetValue(name, out var lazy) ? lazy.Value : null;
	}

	private static Lazy<IMessageBus> CreateLazy(IServiceProvider serviceProvider, string name) =>
			new(() => serviceProvider.GetRequiredKeyedService<IMessageBus>(name),
					LazyThreadSafetyMode.ExecutionAndPublication);
}
