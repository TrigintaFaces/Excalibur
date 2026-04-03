// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Extension methods for bulk transport routing configuration.
/// </summary>
public static class TransportRoutingBuilderExtensions
{
	private const int MaxCacheEntries = 1024;
	private static readonly ConcurrentDictionary<Type, MethodInfo> GenericRouteMethodCache = new();

	private static readonly MethodInfo RouteMethodDefinition =
		typeof(ITransportRoutingBuilder).GetMethod(nameof(ITransportRoutingBuilder.Route))!;

	/// <summary>
	/// Routes all specified message types to the given transport.
	/// </summary>
	/// <param name="builder">The transport routing builder.</param>
	/// <param name="transportName">The target transport name (e.g., "rabbitmq", "kafka").</param>
	/// <param name="messageTypes">
	/// The message types to route. Each must implement <see cref="IIntegrationEvent"/>;
	/// types that do not will cause an <see cref="ArgumentException"/>.
	/// </param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/>, <paramref name="transportName"/>, or <paramref name="messageTypes"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transportName"/> is empty/whitespace, or any element of
	/// <paramref name="messageTypes"/> is null or does not implement <see cref="IIntegrationEvent"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method uses reflection to call <see cref="ITransportRoutingBuilder.Route{TEvent}"/>
	/// for each type. Generic method instances are cached for performance.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// routing.Transport.RouteAll("rabbitmq",
	///     typeof(OrderCreatedEvent),
	///     typeof(PaymentProcessedEvent),
	///     typeof(ShipmentDispatchedEvent));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("RouteAll uses reflection to invoke the generic Route<T>() method.")]
	[RequiresDynamicCode("RouteAll uses MakeGenericMethod to invoke Route<T>() at runtime.")]
	public static ITransportRoutingBuilder RouteAll(
		this ITransportRoutingBuilder builder,
		string transportName,
		params Type[] messageTypes)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(messageTypes);

		foreach (var type in messageTypes)
		{
			ArgumentNullException.ThrowIfNull(type);

			if (!typeof(IIntegrationEvent).IsAssignableFrom(type))
			{
				throw new ArgumentException(
					$"Type '{type.Name}' does not implement IIntegrationEvent. " +
					"Only integration events can be routed to transports.",
					nameof(messageTypes));
			}

			// Get or create the closed generic Route<T>() method
			MethodInfo routeMethod;
			if (GenericRouteMethodCache.TryGetValue(type, out var cached))
			{
				routeMethod = cached;
			}
			else
			{
				routeMethod = RouteMethodDefinition.MakeGenericMethod(type);

				// Bounded cache: skip caching when full to prevent unbounded memory growth
				if (GenericRouteMethodCache.Count < MaxCacheEntries)
				{
					GenericRouteMethodCache.TryAdd(type, routeMethod);
				}
			}

			// Call Route<T>() -> returns ITransportRuleBuilder<T>
			var ruleBuilder = routeMethod.Invoke(builder, null)!;

			// Call To(transportName) on the rule builder
			var toMethod = ruleBuilder.GetType().GetMethod("To")!;
			_ = toMethod.Invoke(ruleBuilder, [transportName]);
		}

		return builder;
	}
}
