// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.Redis;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable Redis fixture backed by a TestContainers <see cref="RedisContainer"/>. Inherit or use
/// directly to test a Redis provider implementation (leader election, fencing tokens, inbox/outbox)
/// against a real Redis engine.
/// </summary>
/// <remarks>
/// Redis is a key-value store, not an ADO.NET database, so this fixture deliberately does not implement
/// <see cref="IDatabaseContainerFixture"/>; it exposes the raw <see cref="ConnectionString"/> that a
/// consumer passes to its own client (e.g. <c>ConnectionMultiplexer.Connect(...)</c>).
/// </remarks>
public class RedisContainerFixture : ContainerFixtureBase
{
	private RedisContainer? _container;

	/// <summary>
	/// Gets the Redis connection string (host:port) for the started container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the container has not been initialized.</exception>
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The Redis container has not been initialized.");

	/// <summary>Gets the Docker image used for the Redis container.</summary>
	protected virtual string Image => "redis:7-alpine";

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RedisBuilder().WithImage(Image).Build();
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
