// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for the secure-by-default event-type allow-list.
/// </summary>
/// <remarks>
/// <para>
/// The default <c>JsonEventSerializer</c> rejects unregistered event types (the deserialization
/// gadget-chain guard) unless the unbounded assembly scan is explicitly opted into. Registering your event types
/// here gives the serializer a <em>secure and functional</em> resolution path: registered types resolve
/// by name without any reflection scan, while an unregistered (attacker-chosen) type stays unresolvable.
/// This mirrors the .NET model (<c>JsonSerializerContext</c> / <c>JsonPolymorphismOptions.DerivedTypes</c>).
/// </para>
/// <example>
/// <code>
/// services.AddDispatch();
/// services.AddEventTypes&lt;OrderPlaced&gt;()
///         .AddEventTypes&lt;OrderShipped&gt;();
/// // or: services.AddEventTypes(typeof(OrderPlaced), typeof(OrderShipped));
/// </code>
/// </example>
/// </remarks>
public static class EventTypeRegistrationServiceCollectionExtensions
{
	/// <summary>
	/// Registers <typeparamref name="TEvent"/> for secure name-based event-type resolution.
	/// </summary>
	/// <typeparam name="TEvent">The event type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddEventTypes<TEvent>(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		GetOrAddRegistry(services).Register(typeof(TEvent));
		return services;
	}

	/// <summary>
	/// Registers the specified event types for secure name-based event-type resolution.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="eventTypes">The event types to register.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="eventTypes"/> (or any element) is <see langword="null"/>.
	/// </exception>
	public static IServiceCollection AddEventTypes(this IServiceCollection services, params Type[] eventTypes)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(eventTypes);

		var registry = GetOrAddRegistry(services);
		foreach (var eventType in eventTypes)
		{
			ArgumentNullException.ThrowIfNull(eventType);
			registry.Register(eventType);
		}

		return services;
	}

	/// <summary>
	/// Registers a stored type name that an event type used to be written under, so events already in
	/// the store keep resolving after the type moves namespace or assembly.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="storedTypeName">
	/// The type name exactly as it appears in the store's event-type column, including the assembly,
	/// version, culture and public key token if they were recorded.
	/// </param>
	/// <param name="eventType">The type that name should now resolve to.</param>
	/// <returns>The service collection, for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Moving an event type between namespaces or assemblies changes the name it is written under, and
	/// every event already stored keeps the old one. Resolution is by exact name, so those events stop
	/// deserializing -- a replay or a projection rebuild fails on data that was written correctly.
	/// </para>
	/// <para>
	/// An alias affects RESOLUTION ONLY: new events are still written under the type's current name, so
	/// the retired name stops spreading and the store converges as new events arrive. Registering the
	/// same stored name twice replaces the earlier mapping.
	/// </para>
	/// <para>
	/// This maps one name to one type. It does not change an event's shape -- if the payload also changed,
	/// register an upcaster as well.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="eventType"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="storedTypeName"/> is null or whitespace.</exception>
	public static IServiceCollection AddEventTypeAlias(
		this IServiceCollection services, string storedTypeName, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(storedTypeName);
		ArgumentNullException.ThrowIfNull(eventType);

		var registry = GetOrAddRegistry(services);
		registry.RegisterAlias(storedTypeName, eventType);

		return services;
	}

	/// <summary>
	/// Registers every <see cref="IDomainEvent"/> type defined in <paramref name="assembly"/> for secure
	/// name-based event-type resolution.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="assembly">The assembly to scan (typically <c>typeof(Program).Assembly</c>).</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <remarks>
	/// This is a compile-time-known, consumer-controlled DI-time scan that registers the discovered event
	/// types into the secure allow-list — categorically different from the runtime reflection scan the
	/// serializer rejects by default. The serializer still resolves only registered types, so the security
	/// guarantee is unchanged; this overload only removes the risk of hand-listing and missing one.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="assembly"/> is <see langword="null"/>.
	/// </exception>
	[RequiresUnreferencedCode("Scans the assembly for IDomainEvent types via reflection, which is not trim-safe. Use AddEventTypes<TEvent>() or AddEventTypes(params Type[]) for a trim/AOT-safe path.")]
	public static IServiceCollection AddEventTypesFromAssembly(this IServiceCollection services, Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);

		var registry = GetOrAddRegistry(services);
		foreach (var type in assembly.GetTypes())
		{
			if (type is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(type))
			{
				registry.Register(type);
			}
		}

		return services;
	}

	// Find the single mutable registry instance (so repeated AddEventTypes calls accumulate into one
	// allow-list), or create + register it. The serializer DI factory consults it via IEventTypeRegistry.
	private static EventTypeRegistry GetOrAddRegistry(IServiceCollection services)
	{
		foreach (var descriptor in services)
		{
			if (descriptor.ServiceType == typeof(IEventTypeRegistry)
				&& descriptor.GetImplementationInstance() is EventTypeRegistry existing)
			{
				return existing;
			}
		}

		var registry = new EventTypeRegistry();
		services.Add(ServiceDescriptor.Singleton<IEventTypeRegistry>(registry));

		// Registered here rather than at an opt-in call site: an event that declares no name is a
		// startup failure for every host that registers any event, and a guard the consumer has to
		// remember to switch on is not a guard for the consumer who forgot the attribute.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, UndeclaredEventNameStartupValidator>());

		return registry;
	}
}
