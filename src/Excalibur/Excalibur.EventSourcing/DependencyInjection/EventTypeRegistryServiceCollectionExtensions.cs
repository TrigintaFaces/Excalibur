// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing.TypeMapping;

using Microsoft.Extensions.Configuration;
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

	/// <summary>
	/// Adds the event type registry for mapping stored event type names to CLR types
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddEventTypeRegistry(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<EventTypeRegistryOptions>()
			.Bind(configuration);

		services.TryAddSingleton<IEventTypeRegistry, EventTypeRegistry>();

		return services;
	}
}
