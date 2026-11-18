// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for the simplified <c>AddRabbitMQTransport(connectionString)</c> overload.
/// </summary>
/// <remarks>
/// Sprint 698 T.6 (j6vml): Verifies the connection string overload delegates
/// to the builder overload and configures options correctly.
/// Connection input uses non-default credentials (live connection validation
/// now rejects guest:guest as insecure).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMQTransportConnectionStringOverloadShould
{
	[Fact]
	public void RegisterRabbitMQOptionsWithConnectionString()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		const string connectionString = "amqps://user:password@localhost:5671";

		_ = services.AddRabbitMQTransport(connectionString);
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<global::RabbitMQ.Client.IConnectionFactory>()
			.ShouldBeOfType<global::RabbitMQ.Client.ConnectionFactory>();

		factory.HostName.ShouldBe("localhost");
		factory.Port.ShouldBe(5671);
		factory.UserName.ShouldBe("user");
		factory.Password.ShouldBe("password");
		factory.Ssl.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddRabbitMQTransport("amqp://localhost"));
	}

	[Fact]
	public void ThrowOnNullConnectionString()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddRabbitMQTransport((string)null!));
	}

	[Fact]
	public void ThrowOnEmptyConnectionString()
	{
		Should.Throw<ArgumentException>(() =>
			new ServiceCollection().AddRabbitMQTransport(""));
	}

	[Fact]
	public void ThrowOnWhitespaceConnectionString()
	{
		Should.Throw<ArgumentException>(() =>
			new ServiceCollection().AddRabbitMQTransport("   "));
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddRabbitMQTransport("amqp://user:password@localhost");

		// Assert
		result.ShouldBeSameAs(services);
	}
}
