// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class RabbitMqLiveConnectionValidationShould
{
	[Theory]
	[InlineData("", 5671)]
	[InlineData("broker.internal", 0)]
	[InlineData("broker.internal", 65536)]
	public void RefuseInvalidPartsOnTheActualFactoryInput(string host, int port)
	{
		var options = SecureParts();
		options.Connection.HostName = host;
		options.Connection.Port = port;

		var error = Should.Throw<OptionsValidationException>(() =>
			RabbitMQTransportServiceCollectionExtensions.CreateConnectionFactory(options));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
		error.OptionsName.ShouldBe("live-input");
		error.Failures.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData("http://app:secret@broker.internal:5671/")]
	[InlineData("not-a-uri")]
	[InlineData("amqps://guest:guest@broker.internal:5671/")]
	public void RefuseInvalidUriOnTheActualFactoryInput(string uri)
	{
		var options = SecureParts();
		options.Connection.ConnectionString = uri;

		var error = Should.Throw<OptionsValidationException>(() =>
			RabbitMQTransportServiceCollectionExtensions.CreateConnectionFactory(options));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
		error.OptionsName.ShouldBe("live-input");
		error.Failures.ShouldNotBeEmpty();
	}

	[Fact]
	public void PreserveUriPrecedenceOverUnusedPartsAndRecoverySettings()
	{
		var options = SecureParts();
		options.Connection.ConnectionString = "amqps://app:secret@uri-broker:5673/production";
		options.Connection.HostName = string.Empty;
		options.Connection.Port = 0;
		options.Connection.Username = "guest";
		options.Connection.Password = "guest";
		options.Connection.UseSsl = false;
		options.Connection.AutomaticRecoveryEnabled = false;
		options.Connection.NetworkRecoveryInterval = TimeSpan.FromSeconds(13);

		var factory = RabbitMQTransportServiceCollectionExtensions.CreateConnectionFactory(options)
			.ShouldBeOfType<ConnectionFactory>();

		factory.HostName.ShouldBe("uri-broker");
		factory.Port.ShouldBe(5673);
		factory.VirtualHost.ShouldBe("production");
		factory.UserName.ShouldBe("app");
		factory.Password.ShouldBe("secret");
		factory.Ssl.Enabled.ShouldBeTrue();
		factory.AutomaticRecoveryEnabled.ShouldBeFalse();
		factory.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(13));
	}

	[Fact]
	public void RemoveTheUnusedPublicConnectionCopy()
	{
		typeof(RabbitMqOptions).GetProperty("Connection").ShouldBeNull();
		typeof(RabbitMqOptions).Assembly.GetType(
			"Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqConnectionOptions").ShouldBeNull();
	}

	private static RabbitMQTransportOptions SecureParts()
	{
		var options = new RabbitMQTransportOptions { Name = "live-input" };
		options.Connection.HostName = "broker.internal";
		options.Connection.Port = 5671;
		options.Connection.Username = "app";
		options.Connection.Password = "secret";
		options.Connection.UseSsl = true;
		return options;
	}
}
