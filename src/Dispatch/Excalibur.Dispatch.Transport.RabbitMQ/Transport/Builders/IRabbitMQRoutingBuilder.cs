// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Builder interface for configuring RabbitMQ message routing and SSL.
/// </summary>
/// <remarks>
/// Provides methods for mapping message types to exchanges and queues,
/// setting name prefixes, and enabling SSL/TLS connections.
/// </remarks>
public interface IRabbitMQRoutingBuilder
{
    /// <summary>
    /// Maps a message type to a specific exchange for routing.
    /// </summary>
    /// <typeparam name="TMessage">The message type to map.</typeparam>
    /// <param name="exchange">The target exchange name.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="exchange"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder MapExchange<TMessage>(string exchange) where TMessage : class;

    /// <summary>
    /// Maps a message type to a specific queue for routing.
    /// </summary>
    /// <typeparam name="TMessage">The message type to map.</typeparam>
    /// <param name="queue">The target queue name.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queue"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder MapQueue<TMessage>(string queue) where TMessage : class;

    /// <summary>
    /// Sets a prefix to be applied to all exchange names.
    /// </summary>
    /// <param name="prefix">The exchange name prefix (e.g., "myapp-prod-").</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder WithExchangePrefix(string prefix);

    /// <summary>
    /// Sets a prefix to be applied to all queue names.
    /// </summary>
    /// <param name="prefix">The queue name prefix (e.g., "myapp-prod-").</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder WithQueuePrefix(string prefix);

    /// <summary>
    /// Enables SSL/TLS for the connection.
    /// </summary>
    /// <param name="configure">Optional action to configure SSL options.</param>
    /// <returns>The builder for chaining.</returns>
    IRabbitMQTransportBuilder UseSsl(Action<RabbitMQSslOptions>? configure = null);
}
