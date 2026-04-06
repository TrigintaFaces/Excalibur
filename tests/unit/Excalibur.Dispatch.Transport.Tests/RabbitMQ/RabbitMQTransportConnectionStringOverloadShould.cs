// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for the simplified <c>AddRabbitMQTransport(connectionString)</c> overload.
/// </summary>
/// <remarks>
/// Sprint 698 T.6 (j6vml): Verifies the connection string overload delegates
/// to the builder overload and configures options correctly.
/// Sprint 750: Updated to use non-default credentials (RabbitMqOptionsValidator
/// now rejects guest:guest as insecure).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMQTransportConnectionStringOverloadShould
{
	[Fact]
	public void RegisterRabbitMQOptionsWithConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string connectionString = "amqp://user:password@localhost:5672";

		// Act
		services.AddRabbitMQTransport(connectionString);
		using var provider = services.BuildServiceProvider();

		// Assert -- the connection string overload delegates to the builder which configures options
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<RabbitMqOptions>>();
		options.ShouldNotBeNull();
		options.Value.Connection.ConnectionString.ShouldBe(connectionString);
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
