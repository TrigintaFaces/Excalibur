// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Binds the two ways this transport may be told where its broker is: an AMQP URI, or the host parts
/// (<c>HostName</c>, <c>Port</c>, <c>VirtualHost</c>, <c>Credentials</c>). Both are public builder
/// methods, and the connection factory honours both -- a URI becomes <c>factory.Uri</c>, and in its
/// absence the parts are assigned individually.
/// </summary>
/// <remarks>
/// Connection structure is checked on the actual captured input during registration and factory creation.
/// TLS and default host-parts credentials remain lazy checks on the internal factory path.
/// Resolving a connection factory does not open a broker connection.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class HostPartsConfigurationIsAcceptedShould
{
	[Fact]
	public void Accept_a_composition_configured_by_host_parts_with_no_connection_string()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// TLS AND NON-DEFAULT CREDENTIALS, because this arm claims the composition would have connected
		// and must therefore be a composition that actually can. An earlier version of it set only the
		// host, port and credentials: that one passes this assertion and then throws
		// TransportSecurityException the moment anything builds a connection, because RequireTls
		// defaults to true. It asserted validation while describing connectability -- which is the gap
		// the liveness arm below now closes rather than merely wording around.
		_ = services.AddRabbitMQTransport(rmq => rmq
			.HostName("broker.internal")
			.Port(5671)
			.VirtualHost("/")
			.UseSsl()
			.Credentials("app-user", "app-secret")); // pragma: allowlist secret

		using var provider = services.BuildServiceProvider();

		// Reading .Value is what runs the validation pipeline, and is what ValidateOnStart() does at
		// host start. An exception here is a consumer whose application does not boot.
		var options = provider.GetRequiredService<IOptions<RabbitMqOptions>>();

		_ = Should.NotThrow(
			() => options.Value,
			"a transport configured by host parts must remain usable: the connection factory assigns the "
			+ "parts individually when no URI was supplied, so refusing the composition rejects a "
			+ "configuration that would have connected");
	}

	[Fact]
	public void Build_a_connection_factory_from_the_host_parts_without_dialling_anything()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddRabbitMQTransport(rmq => rmq
			.HostName("broker.internal")
			.Port(5671)
			.VirtualHost("/")
			.UseSsl()
			.Credentials("app-user", "app-secret")); // pragma: allowlist secret

		using var provider = services.BuildServiceProvider();

		// THE LIVENESS ARM. Validation passing says only that nothing refused the options; it is
		// satisfied by a composition that cannot be connected with at all. This resolves the factory the
		// transport actually dials with and asserts the parts reached it, which is the property the arm
		// above is really about. Resolving the factory constructs it and does not open a socket --
		// connecting happens on CreateConnectionAsync, which nothing here calls.
		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.HostName.ShouldBe("broker.internal");
		factory.Port.ShouldBe(5671);
		factory.UserName.ShouldBe("app-user");
		factory.Ssl.Enabled.ShouldBeTrue(
			"the host-parts path must carry the TLS it was asked for, or the composition this suite "
			+ "calls connectable would be refused by the security posture at the first connection");
	}

	[Fact]
	public void Refuse_the_default_credentials_when_they_arrive_as_host_parts_rather_than_in_a_uri()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Defaults are Username = "guest", Password = "guest", so stating neither is how a consumer
		// reaches this. TLS is enabled so that the refusal under test is the credentials one and not
		// the TLS posture firing first -- an arm that cannot tell which guard refused is not evidence
		// about either.
		_ = services.AddRabbitMQTransport(rmq => rmq
			.HostName("broker.internal")
			.Port(5671)
			.UseSsl());

		using var provider = services.BuildServiceProvider();

		// THE SYMMETRY ARM, and it is the one that matters. The same credentials are refused when they
		// arrive as 'amqp://guest:guest@host' (the arm below). Until this existed they were ACCEPTED in
		// this spelling with only a log line, so the configuration syntax decided whether the posture
		// applied.
		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredService<IConnectionFactory>(),
			"default credentials supplied as host parts must be refused exactly as they are inside a "
			+ "connection string");

		refusal.FailureReason.ShouldBe(
			TransportSecurityFailureReason.DefaultCredentials,
			"the refusal must name the credentials as the cause -- reporting a known cause as "
			+ "Unspecified, or as TlsNotEnabled, tells a consumer to change the wrong setting");
	}

	[Fact]
	public void Still_refuse_a_supplied_connection_string_that_is_not_an_amqp_uri()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var error = Should.Throw<OptionsValidationException>(() =>
			services.AddRabbitMQTransport(rmq => rmq.ConnectionString("http://broker.internal:5672")));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
	}

	[Fact]
	public void Still_refuse_a_supplied_connection_string_carrying_the_default_credentials()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var error = Should.Throw<OptionsValidationException>(() =>
			services.AddRabbitMQTransport(rmq => rmq.ConnectionString("amqp://guest:guest@broker.internal:5672")));

		error.OptionsType.ShouldBe(typeof(RabbitMQConnectionOptions));
	}
}
