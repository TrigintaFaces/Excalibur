// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Builder interface for configuring RabbitMQ topology (exchanges, queues, bindings, dead letters, and CloudEvents).
/// </summary>
/// <remarks>
/// Provides methods for declaring exchanges, queues, bindings, dead letter handling,
/// and CloudEvents options for RabbitMQ transport.
/// </remarks>
public interface IRabbitMQTopologyBuilder
{
    /// <summary>
    /// Configures an exchange for this transport.
    /// </summary>
    /// <param name="configure">The exchange configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// Multiple exchanges can be configured by calling this method multiple times.
    /// </remarks>
    IRabbitMQTransportBuilder ConfigureExchange(Action<IRabbitMQExchangeBuilder> configure);

    /// <summary>
    /// Configures a queue for this transport.
    /// </summary>
    /// <param name="configure">The queue configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// Multiple queues can be configured by calling this method multiple times.
    /// </remarks>
    IRabbitMQTransportBuilder ConfigureQueue(Action<IRabbitMQQueueBuilder> configure);

    /// <summary>
    /// Configures a binding between an exchange and a queue.
    /// </summary>
    /// <param name="configure">The binding configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// Multiple bindings can be configured by calling this method multiple times.
    /// </remarks>
    IRabbitMQTransportBuilder ConfigureBinding(Action<IRabbitMQBindingBuilder> configure);

    /// <summary>
    /// Configures dead letter exchange handling.
    /// </summary>
    /// <param name="configure">The dead letter configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    IRabbitMQTransportBuilder ConfigureDeadLetter(Action<IRabbitMQDeadLetterBuilder> configure);

    /// <summary>
    /// Configures CloudEvents options for the transport.
    /// </summary>
    /// <param name="configure">The CloudEvents configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    IRabbitMQTransportBuilder ConfigureCloudEvents(Action<RabbitMqCloudEventOptions> configure);
}
