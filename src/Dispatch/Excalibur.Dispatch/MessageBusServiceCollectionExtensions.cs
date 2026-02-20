// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering message buses with keyed DI.
/// </summary>
public static class MessageBusServiceCollectionExtensions
{
	/// <summary>
	/// Adds a named message bus to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The message bus name. </param>
	/// <param name="isRemote"> Whether the bus is remote. </param>
	/// <param name="factory"> Factory that creates the bus instance. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddMessageBus(
		this IServiceCollection services,
		string name,
		bool isRemote,
		Func<IServiceProvider, IMessageBus> factory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(factory);

		if (!HasKeyedMessageBus(services, name))
		{
			_ = services.AddKeyedSingleton(name, (sp, _) => factory(sp));
		}

		if (!HasMessageBusRegistration(services, name))
		{
			_ = services.AddSingleton<IMessageBusRegistration>(
				new MessageBusRegistration(name, isRemote));
		}

		return services;
	}

	/// <summary>
	/// Adds a named remote message bus to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The message bus name. </param>
	/// <param name="factory"> Factory that creates the bus instance. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddRemoteMessageBus(
		this IServiceCollection services,
		string name,
		Func<IServiceProvider, IMessageBus> factory) =>
		services.AddMessageBus(name, isRemote: true, factory);

	private static bool HasKeyedMessageBus(IServiceCollection services, string name)
	{
		return services.Any(descriptor =>
			descriptor.ServiceType == typeof(IMessageBus) &&
			descriptor.ServiceKey is string key &&
			string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasMessageBusRegistration(IServiceCollection services, string name)
	{
		return services.Any(descriptor =>
			descriptor.ServiceType == typeof(IMessageBusRegistration) &&
			descriptor.ImplementationInstance is MessageBusRegistration registration &&
			string.Equals(registration.Name, name, StringComparison.OrdinalIgnoreCase));
	}
}
