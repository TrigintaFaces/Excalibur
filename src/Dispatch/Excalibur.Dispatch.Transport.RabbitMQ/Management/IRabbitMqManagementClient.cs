// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Client for interacting with the RabbitMQ Management HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// Provides read and operational access to the RabbitMQ Management plugin's HTTP API
/// for monitoring queue states, exchange configurations, connection details, and
/// performing administrative operations such as queue purging.
/// </para>
/// <para>
/// This interface follows the Microsoft pattern of keeping interface surface area minimal
/// (5 methods). Additional management operations can be added via extension methods
/// or the <see cref="IServiceProvider.GetService(Type)"/> escape hatch for direct HTTP access.
/// </para>
/// </remarks>
public interface IRabbitMqManagementClient : IAsyncDisposable
{
	/// <summary>
	/// Gets information about a specific queue.
	/// </summary>
	/// <param name="queueName">The name of the queue.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="QueueInfo"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="queueName"/> is null.
	/// </exception>
	Task<QueueInfo> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets information about a specific exchange.
	/// </summary>
	/// <param name="exchangeName">The name of the exchange.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="ExchangeInfo"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="exchangeName"/> is null.
	/// </exception>
	Task<ExchangeInfo> GetExchangeInfoAsync(string exchangeName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets information about a specific connection.
	/// </summary>
	/// <param name="connectionName">The name of the connection.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="ConnectionInfo"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionName"/> is null.
	/// </exception>
	Task<ConnectionInfo> GetConnectionInfoAsync(string connectionName, CancellationToken cancellationToken);

	/// <summary>
	/// Purges all messages from the specified queue.
	/// </summary>
	/// <param name="queueName">The name of the queue to purge.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous purge operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="queueName"/> is null.
	/// </exception>
	Task PurgeQueueAsync(string queueName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a high-level overview of the RabbitMQ broker.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="BrokerOverview"/>.
	/// </returns>
	Task<BrokerOverview> GetOverviewAsync(CancellationToken cancellationToken);
}
