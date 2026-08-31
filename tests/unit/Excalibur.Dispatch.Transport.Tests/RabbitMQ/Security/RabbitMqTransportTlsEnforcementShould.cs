// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Security;

/// <summary>
/// Binds the RabbitMQ transport's TLS posture to the path a consumer actually uses: the registration
/// extension, a real container, and a resolve.
/// </summary>
/// <remarks>
/// <para>
/// Every arm here goes through <c>AddRabbitMQTransport</c> and a real <see cref="ServiceProvider"/>.
/// None constructs the connection factory by hand, because hand-construction is exactly what let the
/// previous refusal be correct, tested, and unreachable: it lived in a connection class nothing on the
/// registration path built.
/// </para>
/// <para>
/// Resolving <see cref="IConnectionFactory"/> is the whole surface. Every AMQP client this package
/// creates -- the connection, its channels, the senders, receivers, subscribers, the dead-letter queue
/// manager and both health checks -- is reached through that one registration, so a client that evaded
/// the refusal would have to be built without a connection factory.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqTransportTlsEnforcementShould : UnitTestBase
{
	private const string SecurePlaintextUri = "amqp://appuser:S3cretPw0rd@broker.example.com:5672/";
	private const string SecureTlsUri = "amqps://appuser:S3cretPw0rd@broker.example.com:5671/";

	[Fact]
	public async Task RefuseToBuildAConnectionFactory_WhenTheConnectionStringIsPlaintext()
	{
		await using var provider = Build(rmq => rmq.ConnectionString(SecurePlaintextUri));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredService<IConnectionFactory>());

		refusal.Message.ShouldContain("TLS is required");

		// A caller that branches on the reason must not read Unspecified from a TLS refusal.
		refusal.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
		refusal.TransportName.ShouldBe("RabbitMQ");
	}

	[Fact]
	public async Task RefuseToBuildAConnectionFactory_WhenHostAndPortAreConfiguredWithoutSsl()
	{
		await using var provider = Build(rmq => rmq
			.HostName("broker.example.com")
			.Port(5672)
			.Credentials("appuser", "S3cretPw0rd"));

		_ = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredService<IConnectionFactory>());
	}

	/// <summary>
	/// The refusal has to reach the clients, not merely the factory. This resolves a channel -- two
	/// registrations downstream of the factory -- and asserts the same refusal surfaces there.
	/// </summary>
	[Fact]
	public async Task RefuseToBuildAChannel_WhenTheConnectionIsPlaintext()
	{
		await using var provider = Build(rmq => rmq.ConnectionString(SecurePlaintextUri));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredService<IChannel>());

		refusal.Message.ShouldContain("TLS is required");
	}

	[Fact]
	public async Task BuildAConnectionFactoryThatCarriesTls_WhenTheConnectionStringUsesAmqps()
	{
		await using var provider = Build(rmq => rmq.ConnectionString(SecureTlsUri));

		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.Ssl.Enabled.ShouldBeTrue("the amqps scheme is one of the two spellings of TLS here");
	}

	[Fact]
	public async Task BuildAConnectionFactoryThatCarriesTls_WhenUseSslIsConfigured()
	{
		await using var provider = Build(rmq => rmq
			.HostName("broker.example.com")
			.Port(5671)
			.Credentials("appuser", "S3cretPw0rd")
			.UseSsl());

		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.Ssl.Enabled.ShouldBeTrue();
	}

	/// <summary>
	/// The documented escape hatch has to work, or the refusal is not a posture but a ban. A local
	/// broker with no certificate is a real configuration; it just has to be asked for.
	/// </summary>
	[Fact]
	public async Task BuildAPlaintextConnectionFactory_WhenTlsIsExplicitlyNotRequired()
	{
		await using var provider = Build(rmq => rmq
			.ConnectionString(SecurePlaintextUri)
			.RequireTls(false));

		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.Ssl.Enabled.ShouldBeFalse("opting out must actually opt out, not merely be accepted");
	}

	/// <summary>
	/// An amqps connection string sets the name the peer certificate is verified against. Configuring
	/// the certificate settings alongside it must add to that, never replace it: an empty expected name
	/// matches no certificate, so the handshake would fail for a reason nobody configured.
	/// </summary>
	[Fact]
	public async Task KeepTheServerNameFromTheConnectionString_WhenUseSslAddsCertificateSettings()
	{
		await using var provider = Build(rmq => rmq
			.ConnectionString(SecureTlsUri)
			.UseSsl(ssl => ssl.CertificatePath = "/etc/ssl/client.p12"));

		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.Ssl.Enabled.ShouldBeTrue();
		factory.Ssl.ServerName.ShouldBe("broker.example.com");
		factory.Ssl.CertPath.ShouldBe("/etc/ssl/client.p12");
	}

	/// <summary>
	/// With no connection string to take it from, the expected peer name defaults to the host being
	/// dialled. Leaving it empty fails every handshake, which reads as a broker fault rather than as a
	/// setting nobody supplied.
	/// </summary>
	[Fact]
	public async Task DefaultTheServerNameToTheHost_WhenUseSslSuppliesNoServerName()
	{
		await using var provider = Build(rmq => rmq
			.HostName("broker.example.com")
			.Port(5671)
			.Credentials("appuser", "S3cretPw0rd")
			.UseSsl());

		var factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

		factory.Ssl.ServerName.ShouldBe("broker.example.com");
	}

	private static ServiceProvider Build(Action<IRabbitMQTransportBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRabbitMQTransport("tls-posture", configure);

		return services.BuildServiceProvider();
	}
}
