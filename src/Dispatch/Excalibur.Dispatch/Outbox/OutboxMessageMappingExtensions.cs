// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Builder for configuring message routing to multiple transports.
/// </summary>
public interface IMessageRoutingBuilder
{
	/// <summary>
	/// Configures a message type to be routed to specific transports.
	/// </summary>
	/// <typeparam name="TMessage">The message type.</typeparam>
	/// <returns>A builder for configuring transport targets.</returns>
	IMessageRouteBuilder<TMessage> RouteMessage<TMessage>() where TMessage : class;

	/// <summary>
	/// Configures the default transports for messages without specific routing.
	/// </summary>
	/// <param name="transports">The default transport names.</param>
	/// <returns>This builder for fluent configuration.</returns>
	IMessageRoutingBuilder WithDefaultTransports(params string[] transports);
}

/// <summary>
/// Builder for configuring transport targets for a specific message type.
/// </summary>
/// <typeparam name="TMessage">The message type being configured.</typeparam>
public interface IMessageRouteBuilder<TMessage> where TMessage : class
{
	/// <summary>
	/// Routes this message type to the specified transports.
	/// </summary>
	/// <param name="transports">The target transport names.</param>
	/// <returns>The parent builder for fluent configuration.</returns>
	IMessageRoutingBuilder ToTransports(params string[] transports);

	/// <summary>
	/// Routes this message type to all configured default transports.
	/// </summary>
	/// <returns>The parent builder for fluent configuration.</returns>
	IMessageRoutingBuilder ToAllDefaults();
}

/// <summary>
/// Extension methods for configuring outbox message mapping via dependency injection.
/// </summary>
public static class OutboxMessageMappingExtensions
{
	/// <summary>
	/// Adds outbox message mapping services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOutboxMessageMapping(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IOutboxMessageMapper>(sp =>
		{
			var messageMapper = sp.GetService<IMessageMapper>();
			var mapperRegistry = sp.GetService<IMessageMapperRegistry>();
			var routingConfig = sp.GetService<IMessageRoutingConfiguration>();

			return new OutboxMessageMapper(messageMapper, mapperRegistry, routingConfig);
		});

		return services;
	}

	/// <summary>
	/// Adds outbox message mapping to the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOutboxMessageMapping(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOutboxMessageMapping();
		return builder;
	}

	/// <summary>
	/// Configures the outbox to use message mapping when publishing messages.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure message routing.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseOutboxMessageMapping(
		this IDispatchBuilder builder,
		Action<IMessageRoutingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var routingBuilder = new MessageRoutingBuilder();
		configure(routingBuilder);

		var routingConfig = routingBuilder.Build();
		_ = builder.Services.AddSingleton(routingConfig);
		_ = builder.Services.AddOutboxMessageMapping();

		return builder;
	}
}

/// <summary>
/// Default implementation of <see cref="IMessageRoutingBuilder"/>.
/// </summary>
internal sealed class MessageRoutingBuilder : IMessageRoutingBuilder
{
	private readonly Dictionary<string, List<string>> _routes = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<string> _defaultTransports = [];

	/// <summary>
	/// Gets the default transports.
	/// </summary>
	internal IReadOnlyList<string> DefaultTransports => _defaultTransports;

	/// <inheritdoc/>
	public IMessageRouteBuilder<TMessage> RouteMessage<TMessage>() where TMessage : class
	{
		return new MessageRouteBuilder<TMessage>(this);
	}

	/// <inheritdoc/>
	public IMessageRoutingBuilder WithDefaultTransports(params string[] transports)
	{
		ArgumentNullException.ThrowIfNull(transports);
		_defaultTransports.Clear();
		_defaultTransports.AddRange(transports);
		return this;
	}

	/// <summary>
	/// Builds the routing configuration.
	/// </summary>
	public IMessageRoutingConfiguration Build()
	{
		return new MessageRoutingConfiguration(_routes, _defaultTransports);
	}

	/// <summary>
	/// Adds a route for a message type.
	/// </summary>
	internal void AddRoute(string messageType, IEnumerable<string> transports)
	{
		_routes[messageType] = [.. transports];
	}
}

/// <summary>
/// Default implementation of <see cref="IMessageRouteBuilder{TMessage}"/>.
/// </summary>
internal sealed class MessageRouteBuilder<TMessage> : IMessageRouteBuilder<TMessage>
	where TMessage : class
{
	private readonly MessageRoutingBuilder _parent;

	public MessageRouteBuilder(MessageRoutingBuilder parent)
	{
		_parent = parent ?? throw new ArgumentNullException(nameof(parent));
	}

	/// <inheritdoc/>
	public IMessageRoutingBuilder ToTransports(params string[] transports)
	{
		ArgumentNullException.ThrowIfNull(transports);
		var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
		_parent.AddRoute(messageType, transports);
		return _parent;
	}

	/// <inheritdoc/>
	public IMessageRoutingBuilder ToAllDefaults()
	{
		var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
		_parent.AddRoute(messageType, _parent.DefaultTransports);
		return _parent;
	}
}

/// <summary>
/// Default implementation of <see cref="IMessageRoutingConfiguration"/>.
/// </summary>
internal sealed class MessageRoutingConfiguration : IMessageRoutingConfiguration
{
	private readonly Dictionary<string, IReadOnlyCollection<string>> _routes;
	private readonly IReadOnlyCollection<string> _defaultTransports;

	public MessageRoutingConfiguration(
		IDictionary<string, List<string>> routes,
		IEnumerable<string> defaultTransports)
	{
		ArgumentNullException.ThrowIfNull(routes);
		ArgumentNullException.ThrowIfNull(defaultTransports);

		_routes = routes.ToDictionary(
			kvp => kvp.Key,
			kvp => (IReadOnlyCollection<string>)kvp.Value.AsReadOnly(),
			StringComparer.OrdinalIgnoreCase);
		_defaultTransports = defaultTransports.ToList().AsReadOnly();
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<string> DefaultTransports => _defaultTransports;

	/// <inheritdoc/>
	public IReadOnlyCollection<string> GetTargetTransports(string messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

		if (_routes.TryGetValue(messageType, out var transports))
		{
			return transports;
		}

		return _defaultTransports;
	}
}
