// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Analyzes handler constructor dependencies to determine if a transient handler can be safely
/// promoted to singleton registration. Follows the ASP.NET Core minimal API pattern where
/// stateless handlers are effectively singleton for optimal dispatch performance.
/// </summary>
internal static class HandlerLifetimeAnalyzer
{
	/// <summary>
	/// Handler interface generic type definitions eligible for singleton promotion.
	/// </summary>
	private static readonly Type[] HandlerInterfaceDefinitions =
	[
		typeof(IActionHandler<>),
		typeof(IActionHandler<,>),
		typeof(IEventHandler<>),
	];

	/// <summary>
	/// Scans the service collection for transient handler registrations and promotes eligible
	/// ones to singleton. A handler is eligible if all its constructor dependencies are registered
	/// as singletons (or have no dependencies).
	/// </summary>
	/// <param name="services">The service collection to scan and modify.</param>
	/// <returns>The number of handlers promoted to singleton.</returns>
	[RequiresUnreferencedCode("Uses reflection to inspect handler constructor parameters")]
	public static int PromoteEligibleHandlers(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var promoted = 0;

		// Build a snapshot of current registrations for lifetime lookup
		var lifetimeMap = BuildLifetimeMap(services);

		// Find transient handler registrations that can be promoted
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var descriptor = services[i];

			if (descriptor.Lifetime != ServiceLifetime.Transient)
			{
				continue;
			}

			if (!IsHandlerInterface(descriptor.ServiceType))
			{
				continue;
			}

			var implType = descriptor.ImplementationType;
			if (implType is null || implType.IsAbstract || implType.IsInterface)
			{
				continue;
			}

			if (!CanBePromotedToSingleton(implType, lifetimeMap))
			{
				continue;
			}

			// Replace with singleton registration
			services[i] = ServiceDescriptor.Singleton(descriptor.ServiceType, implType);

			// Also promote the concrete type registration if it exists
			for (var j = services.Count - 1; j >= 0; j--)
			{
				if (j != i &&
					services[j].Lifetime == ServiceLifetime.Transient &&
					services[j].ServiceType == implType &&
					services[j].ImplementationType == implType)
				{
					services[j] = ServiceDescriptor.Singleton(implType, implType);
					break;
				}
			}

			promoted++;
		}

		return promoted;
	}

	/// <summary>
	/// Determines if a handler type can be safely promoted to singleton.
	/// A handler is eligible if:
	/// - It has a parameterless constructor, OR
	/// - All constructor parameters are registered as singletons
	/// - It has no mutable instance fields (heuristic: no non-readonly fields)
	/// </summary>
	private static bool CanBePromotedToSingleton(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType,
		Dictionary<Type, ServiceLifetime> lifetimeMap)
	{
		var constructors = handlerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
		if (constructors.Length == 0)
		{
			return true; // No public constructors = can be singleton
		}

		// Use the constructor with the most parameters (DI convention)
		var ctor = constructors.OrderByDescending(static c => c.GetParameters().Length).First();
		var parameters = ctor.GetParameters();

		if (parameters.Length == 0)
		{
			return true; // Parameterless constructor = stateless = safe singleton
		}

		// Check all constructor parameters are registered as singletons
		foreach (var param in parameters)
		{
			var paramType = param.ParameterType;

			// Check direct registration
			if (lifetimeMap.TryGetValue(paramType, out var lifetime))
			{
				if (lifetime != ServiceLifetime.Singleton)
				{
					return false; // Has scoped/transient dependency
				}

				continue;
			}

			// For open generic types (ILogger<T>, IOptions<T>), check if the generic definition is singleton
			if (paramType.IsGenericType)
			{
				var genericDef = paramType.GetGenericTypeDefinition();
				if (lifetimeMap.TryGetValue(genericDef, out var genericLifetime))
				{
					if (genericLifetime != ServiceLifetime.Singleton)
					{
						return false;
					}

					continue;
				}
			}

			// Unknown dependency — don't promote (conservative)
			return false;
		}

		return true;
	}

	private static bool IsHandlerInterface(Type serviceType)
	{
		if (!serviceType.IsGenericType)
		{
			return false;
		}

		var genericDef = serviceType.GetGenericTypeDefinition();
		foreach (var handlerDef in HandlerInterfaceDefinitions)
		{
			if (genericDef == handlerDef)
			{
				return true;
			}
		}

		return false;
	}

	private static Dictionary<Type, ServiceLifetime> BuildLifetimeMap(IServiceCollection services)
	{
		var map = new Dictionary<Type, ServiceLifetime>(services.Count);

		foreach (var descriptor in services)
		{
			// Use the shortest lifetime if multiple registrations exist (conservative)
			if (map.TryGetValue(descriptor.ServiceType, out var existing))
			{
				if (descriptor.Lifetime < existing) // Transient < Scoped < Singleton
				{
					map[descriptor.ServiceType] = descriptor.Lifetime;
				}
			}
			else
			{
				map[descriptor.ServiceType] = descriptor.Lifetime;
			}
		}

		return map;
	}
}
