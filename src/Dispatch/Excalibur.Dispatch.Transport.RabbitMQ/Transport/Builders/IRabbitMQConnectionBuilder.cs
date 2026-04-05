// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Builder interface for configuring RabbitMQ connection settings.
/// </summary>
/// <remarks>
/// Provides methods for setting the host, port, virtual host, credentials,
/// and connection string for RabbitMQ transport connections.
/// </remarks>
public interface IRabbitMQConnectionBuilder
{
    /// <summary>
    /// Sets the RabbitMQ host name.
    /// </summary>
    /// <param name="hostName">The host name or IP address.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hostName"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder HostName(string hostName);

    /// <summary>
    /// Sets the RabbitMQ port.
    /// </summary>
    /// <param name="port">The port number (typically 5672 for AMQP, 5671 for AMQPS).</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is not between 1 and 65535.</exception>
    IRabbitMQTransportBuilder Port(int port);

    /// <summary>
    /// Sets the virtual host.
    /// </summary>
    /// <param name="vhost">The virtual host path.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="vhost"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder VirtualHost(string vhost);

    /// <summary>
    /// Sets the credentials for authentication.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="username"/> is null or whitespace.</exception>
    IRabbitMQTransportBuilder Credentials(string username, string password);

    /// <summary>
    /// Sets the AMQP connection string (alternative to individual connection properties).
    /// </summary>
    /// <param name="connectionString">The AMQP connection string (e.g., "amqp://user:pass@host:port/vhost").</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or whitespace.</exception>
    /// <remarks>
    /// When a connection string is provided, it takes precedence over individual
    /// <see cref="HostName"/>, <see cref="Port"/>, <see cref="VirtualHost"/>, and <see cref="Credentials"/> settings.
    /// </remarks>
    IRabbitMQTransportBuilder ConnectionString(string connectionString);
}
