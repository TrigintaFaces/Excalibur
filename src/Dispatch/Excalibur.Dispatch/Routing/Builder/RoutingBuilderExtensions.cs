// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing.Builder;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring unified routing in the Excalibur framework.
/// </summary>
public static class RoutingBuilderExtensions
{
	/// <summary>
	/// Configures unified message routing using the fluent routing builder.
	/// </summary>
	/// <param name="builder">The dispatch builder to configure.</param>
	/// <param name="configure">Action to configure routing rules.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method provides a unified entry point for configuring both transport selection
	/// and endpoint routing. It registers the necessary services and applies the routing
	/// configuration.
	/// </para>
	/// <para>
	/// The fluent API supports two-tier routing:
	/// <list type="bullet">
	/// <item><strong>Transport tier</strong>: Determines which message bus handles the message</item>
	/// <item><strong>Endpoint tier</strong>: Determines which services receive the message</item>
	/// </list>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseRouting(routing =>
	///     {
	///         // Transport routing - which message bus?
	///         routing.Transport
	///             .Route&lt;OrderCreated&gt;().To("rabbitmq")
	///             .Route&lt;PaymentProcessed&gt;().To("kafka")
	///             .Default("local");
	///
	///         // Endpoint routing - which services?
	///         routing.Endpoints
	///             .Route&lt;OrderCreated&gt;()
	///                 .To("billing-service", "inventory-service")
	///                 .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
	///
	///         // Optional: Configure fallback for unmatched messages
	///         routing.Fallback.To("dead-letter-queue");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseRouting(
		this IDispatchBuilder builder,
		Action<IRoutingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure the routing builder
		var routingBuilder = new RoutingBuilder();
		configure(routingBuilder);

		// Create routing configuration from the builder
		var configuration = new RoutingConfiguration(routingBuilder);

		// Register the transport selector
		builder.Services.TryAddSingleton<ITransportSelector>(sp =>
			new ConfiguredTransportSelector(configuration));

		// Register the endpoint router
		builder.Services.TryAddSingleton<IEndpointRouter>(sp =>
			new ConfiguredEndpointRouter(configuration));

		// Register the unified dispatch router
		builder.Services.TryAddSingleton<IDispatchRouter>(sp =>
		{
			var transportSelector = sp.GetRequiredService<ITransportSelector>();
			var endpointRouter = sp.GetRequiredService<IEndpointRouter>();
			return new DefaultDispatchRouter(transportSelector, endpointRouter);
		});

		return builder;
	}
}
