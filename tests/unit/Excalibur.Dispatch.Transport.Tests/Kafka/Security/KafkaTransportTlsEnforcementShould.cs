// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Security;

/// <summary>
/// Binds the TLS security posture to the registration path a consumer actually uses.
/// </summary>
/// <remarks>
/// Every arm here goes through <c>AddKafkaTransport</c>, a real <see cref="ServiceProvider"/>, and a
/// real <c>GetRequiredService</c>. Constructing the client by hand would prove only that the check
/// behaves when called, which is the exact gap these tests exist to close: the enforcement has to run
/// where the consumer's producer and consumer are built, not merely where a test can reach it.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaTransportTlsEnforcementShould
{
	[Fact]
	public void RefuseToBuildTheProducerWhenNothingIsConfigured()
	{
		using var provider = BuildProvider(kafka => kafka.BootstrapServers("broker:9092"));

		var exception = Should.Throw<TransportSecurityException>(
			provider.GetRequiredService<IProducer<string, byte[]>>);

		exception.Message.ShouldContain("TLS is required");

		// A caller that branches on the reason must not read Unspecified from a TLS refusal.
		exception.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
		exception.TransportName.ShouldBe("Kafka");
	}

	[Fact]
	public void RefuseToBuildTheConsumerWhenNothingIsConfigured()
	{
		using var provider = BuildProvider(kafka => kafka.BootstrapServers("broker:9092"));

		_ = Should.Throw<TransportSecurityException>(
			provider.GetRequiredService<IConsumer<string, byte[]>>);
	}

	[Fact]
	public void RefuseToBuildTheProducerWhenAPlaintextProtocolIsChosenDeliberately()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.UseSecurityProtocol(SecurityProtocol.SaslPlaintext));

		var exception = Should.Throw<TransportSecurityException>(
			provider.GetRequiredService<IProducer<string, byte[]>>);

		exception.Message.ShouldContain("SaslPlaintext");
	}

	[Fact]
	public void BuildTheProducerWhenTheConfiguredProtocolCarriesTls()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.UseSecurityProtocol(SecurityProtocol.Ssl));

		using var producer = provider.GetRequiredService<IProducer<string, byte[]>>();

		producer.ShouldNotBeNull();
	}

	[Fact]
	public void HonorTheRawConfigurationKeyAsTheOnlySourceOfTheProtocol()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.ConfigureProducer(producer => producer.WithConfig("security.protocol", "sasl_ssl")));

		var options = ResolveOptions(provider);

		KafkaSecurityPosture.ResolveProtocol(options).ShouldBe(SecurityProtocol.SaslSsl);
		_ = Should.NotThrow(() => KafkaConsumerConfigBuilder.Build(options));
	}

	[Fact]
	public void RefuseWhenTheTypedPropertyAndTheRawConfigurationKeyDisagree()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.UseSecurityProtocol(SecurityProtocol.Ssl)
			.ConfigureProducer(producer => producer.WithConfig("security.protocol", "plaintext")));

		var exception = Should.Throw<TransportSecurityException>(
			provider.GetRequiredService<IProducer<string, byte[]>>);

		exception.Message.ShouldContain("Ssl");
		exception.Message.ShouldContain("plaintext");
	}

	[Fact]
	public void RefuseWhenTheRawConfigurationKeyIsNotAProtocolAtAll()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.ConfigureProducer(producer => producer.WithConfig("security.protocol", "tls")));

		_ = Should.Throw<TransportSecurityException>(
			provider.GetRequiredService<IProducer<string, byte[]>>);
	}

	[Fact]
	public void BuildAPlaintextProducerOnlyWhenTheConsumerOptsOutExplicitly()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.RequireTls(false));

		using var producer = provider.GetRequiredService<IProducer<string, byte[]>>();

		producer.ShouldNotBeNull();
	}

	[Fact]
	public void CarryTheResolvedProtocolOntoTheDeadLetterConsumerConfiguration()
	{
		using var provider = BuildProvider(kafka => kafka
			.BootstrapServers("broker:9092")
			.UseSecurityProtocol(SecurityProtocol.Ssl));

		var config = KafkaConsumerConfigBuilder.Build(ResolveOptions(provider));

		config.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
	}

	private static KafkaOptions ResolveOptions(ServiceProvider provider) =>
		provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<KafkaOptions>>()
			.Get(KafkaTransportServiceCollectionExtensions.DefaultTransportName);

	private static ServiceProvider BuildProvider(Action<IKafkaTransportBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKafkaTransport(configure);
		return services.BuildServiceProvider();
	}
}
