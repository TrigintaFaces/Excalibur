// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Extension methods for <see cref="IMessageBusProvider"/>.
/// </summary>
public static class MessageBusProviderExtensions
{
	/// <summary>Gets all registered remote message buses.</summary>
	public static IEnumerable<IMessageBus> GetAllRemoteMessageBuses(this IMessageBusProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IRemoteMessageBusProvider remote)
		{
			return remote.GetAllRemoteMessageBuses();
		}
		return [];
	}

	/// <summary>Gets the names of all registered remote message buses.</summary>
	public static IEnumerable<string> GetAllRemoteMessageBusNames(this IMessageBusProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IRemoteMessageBusProvider remote)
		{
			return remote.GetAllRemoteMessageBusNames();
		}
		return [];
	}

	/// <summary>Attempts to get a remote message bus by name.</summary>
	public static bool TryGetRemote(this IMessageBusProvider provider, string name, out IMessageBus? bus)
	{
		ArgumentNullException.ThrowIfNull(provider);
		if (provider is IRemoteMessageBusProvider remote)
		{
			return remote.TryGetRemote(name, out bus);
		}
		bus = provider.GetRemoteMessageBus(name);
		return bus is not null;
	}
}
