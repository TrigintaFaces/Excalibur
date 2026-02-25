// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.TypeMapping;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the event type registry.
/// </summary>
public static class EventTypeRegistryServiceCollectionExtensions
{
	/// <summary>
	/// Adds the event type registry for mapping stored event type names to CLR types.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration for aliases and explicit type mappings.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// The event type registry enables event type renames by mapping old stored names to current
	/// CLR types. Configure aliases for renamed events:
	/// <code>
	/// services.AddEventTypeRegistry(options =>
	/// {
	///     // Map old event type name to current name
	///     options.Aliases["OrderCreated"] = "OrderPlaced";
	///
	///     // Register explicit type mappings
	///     options.TypeMappings["OrderPlaced"] = typeof(OrderPlacedEvent);
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddEventTypeRegistry(
		this IServiceCollection services,
		Action<EventTypeRegistryOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddSingleton<IEventTypeRegistry, EventTypeRegistry>();

		return services;
	}
}
