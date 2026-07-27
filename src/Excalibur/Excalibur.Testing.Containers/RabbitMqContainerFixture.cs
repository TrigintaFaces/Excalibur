// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.RabbitMq;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable RabbitMQ fixture backed by a TestContainers <see cref="RabbitMqContainer"/>. Inherit or use
/// directly to test a RabbitMQ transport implementation against a real broker.
/// </summary>
/// <remarks>
/// RabbitMQ is a message broker, not an ADO.NET database, so this fixture deliberately does not implement
/// <see cref="IDatabaseContainerFixture"/>; it exposes the AMQP <see cref="ConnectionString"/> that a
/// consumer passes to its own client (e.g. <c>new ConnectionFactory { Uri = new Uri(ConnectionString) }</c>).
/// </remarks>
public class RabbitMqContainerFixture : ContainerFixtureBase
{
	private RabbitMqContainer? _container;

	/// <summary>
	/// Gets the AMQP connection string (URI) for the started container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the container has not been initialized.</exception>
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The RabbitMQ container has not been initialized.");

	/// <summary>Gets the Docker image used for the RabbitMQ container.</summary>
	protected virtual string Image => "rabbitmq:3.13-management-alpine";

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RabbitMqBuilder().WithImage(Image).Build();
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
