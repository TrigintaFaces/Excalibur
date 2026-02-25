// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Tests.Shared;

/// <summary>
/// Base class for message broker integration tests using TestContainers.
/// </summary>
/// <typeparam name="TFixture">The message broker fixture type.</typeparam>
public abstract class MessageBrokerIntegrationTestBase<TFixture> : IntegrationTestBase, IClassFixture<TFixture>
	where TFixture : class, IAsyncLifetime
{
	protected MessageBrokerIntegrationTestBase(TFixture fixture)
	{
		Fixture = fixture;
	}

	protected TFixture Fixture { get; }

	/// <summary>
	/// Gets the connection string or URI from the fixture.
	/// </summary>
	protected abstract string ConnectionString { get; }

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await SetupQueuesAsync();
	}

	public override async Task DisposeAsync()
	{
		await PurgeQueuesAsync();
		await base.DisposeAsync();
	}

	/// <summary>
	/// Initialize services and build provider. Call in derived class constructor after setting up services.
	/// </summary>
	protected void InitializeServices()
	{
		ConfigureServices(Services);
		BuildServiceProvider();
	}

	/// <summary>
	/// Configure message broker-specific services.
	/// </summary>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		// Override to register message broker clients, producers, consumers
	}

	/// <summary>
	/// Create test queues/topics before tests.
	/// </summary>
	protected virtual Task SetupQueuesAsync() => Task.CompletedTask;

	/// <summary>
	/// Purge test queues after tests.
	/// </summary>
	protected virtual Task PurgeQueuesAsync() => Task.CompletedTask;
}

/// <summary>
/// RabbitMQ integration test base class.
/// </summary>
public abstract class RabbitMqIntegrationTestBase : MessageBrokerIntegrationTestBase<RabbitMqContainerFixture>
{
	protected RabbitMqIntegrationTestBase(RabbitMqContainerFixture fixture) : base(fixture)
	{
	}

	protected override string ConnectionString => Fixture.ConnectionString;
}

/// <summary>
/// Kafka integration test base class.
/// </summary>
public abstract class KafkaIntegrationTestBase : MessageBrokerIntegrationTestBase<KafkaContainerFixture>
{
	protected KafkaIntegrationTestBase(KafkaContainerFixture fixture) : base(fixture)
	{
	}

	protected override string ConnectionString => Fixture.BootstrapServers;
}
