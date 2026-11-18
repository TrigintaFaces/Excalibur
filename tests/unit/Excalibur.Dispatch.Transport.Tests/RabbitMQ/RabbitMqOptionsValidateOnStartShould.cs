// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Validates the live connection input at registration and projects a secure factory without dialing.
/// Existing test identities are retained; the deleted shadow-options validator no longer owns this check.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RabbitMqOptionsValidateOnStartShould : UnitTestBase
{
	[Fact]
	public void RejectMalformedConnectionString()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var error = Should.Throw<OptionsValidationException>(() =>
			services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("not-a-uri")));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
		error.OptionsName.ShouldBe("test");
	}

	[Fact]
	public void RejectDefaultGuestCredentials()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var error = Should.Throw<OptionsValidationException>(() =>
			services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqp://guest:guest@localhost:5672/")));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
		error.OptionsName.ShouldBe("test");
	}

	[Fact]
	public void AcceptValidSecureConfiguration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqps://appuser:S3cretPw0rd@localhost:5671/"));
		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<global::RabbitMQ.Client.IConnectionFactory>()
			.ShouldBeOfType<global::RabbitMQ.Client.ConnectionFactory>();

		factory.HostName.ShouldBe("localhost");
		factory.Port.ShouldBe(5671);
		factory.UserName.ShouldBe("appuser");
		factory.Ssl.Enabled.ShouldBeTrue();
	}

	[Theory]
	[InlineData("", 0)]
	[InlineData("", -1)]
	[InlineData("test", 0)]
	[InlineData("test", -1)]
	[Trait(TraitNames.Pattern, "Regression")]
	public void RejectNonPositiveRetainedPayloadLimit(string optionsName, int maxPayloadBytes)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqps://appuser:S3cretPw0rd@localhost:5671/"));
		services.Configure<RabbitMqOptions>(optionsName, options => options.Consumption.MaxPayloadBytes = maxPayloadBytes);
		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get(optionsName));

		error.OptionsType.ShouldBe(typeof(RabbitMqOptions));
		error.OptionsName.ShouldBe(optionsName);
		error.Failures.ShouldContain("RabbitMQ Consumption.MaxPayloadBytes must be null or greater than zero.");
	}

	[Theory]
	[InlineData("", null)]
	[InlineData("", 1)]
	[InlineData("test", null)]
	[InlineData("test", 1)]
	[Trait(TraitNames.Pattern, "Regression")]
	public void PreserveNullAndPositiveRetainedPayloadLimit(string optionsName, int? maxPayloadBytes)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRabbitMQTransport("test", rmq => rmq.ConnectionString("amqps://appuser:S3cretPw0rd@localhost:5671/"));
		services.Configure<RabbitMqOptions>(optionsName, options => options.Consumption.MaxPayloadBytes = maxPayloadBytes);
		using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get(optionsName);

		options.Consumption.MaxPayloadBytes.ShouldBe(maxPayloadBytes);
	}
}
