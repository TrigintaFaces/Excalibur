// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;
using Excalibur.Dispatch.Transport.IbmMq;
using Excalibur.Dispatch.Transport.Mqtt;
using Excalibur.Dispatch.Transport.Pulsar;

using Grpc.Net.Client;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Locks the secure-by-default posture on the four self-hosted transports — gRPC, IBM MQ, MQTT and
/// Pulsar — whose brokers can be reached in the clear.
/// </summary>
/// <remarks>
/// <para>
/// Each transport is exercised through its own public registration entry point and a real
/// <see cref="ServiceProvider"/>, because that is the path a consumer takes; a posture proven only against
/// a directly-constructed options object would not show that the registration reaches it.
/// </para>
/// <para>
/// Both directions are asserted per transport. The refusal arm is the safety property and is RED if the
/// posture is removed or made opt-in. The acceptance arm is the liveness property: it proves the refusal
/// is conditional on the configuration rather than a component that never resolves, and it proves the
/// documented opt-out is reachable from the public surface.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class TransportTlsPostureShould
{
	private const string TransportName = "orders";

	[Fact]
	public void RefuseAPlaintextMqttBroker()
	{
		using var provider = BuildProvider(services => services.AddMqttTransport(
			TransportName,
			mqtt => Configure(mqtt)));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredKeyedService<IMqttConnectionProvider>(TransportName));

		refusal.TransportName.ShouldBe("MQTT");
		refusal.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
	}

	[Fact]
	public void AcceptAnMqttBrokerOverTlsAndHonourTheDocumentedOptOut()
	{
		using var secure = BuildProvider(services => services.AddMqttTransport(
			TransportName,
			mqtt =>
			{
				Configure(mqtt);
				mqtt.UseTls = true;
			}));

		secure.GetRequiredKeyedService<IMqttConnectionProvider>(TransportName).ShouldNotBeNull();

		using var optedOut = BuildProvider(services => services.AddMqttTransport(
			TransportName,
			mqtt =>
			{
				Configure(mqtt);
				mqtt.RequireTls = false;
			}));

		optedOut.GetRequiredKeyedService<IMqttConnectionProvider>(TransportName).ShouldNotBeNull();
	}

	[Fact]
	public void RefuseAnIbmMqChannelWithNoCipherSpec()
	{
		using var provider = BuildProvider(services => services.AddIbmMqTransport(
			TransportName,
			mq => Configure(mq)));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredKeyedService<IIbmMqConnectionProvider>(TransportName));

		refusal.TransportName.ShouldBe("IBM MQ");
		refusal.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
	}

	[Fact]
	public void AcceptAnIbmMqChannelWithACipherSpecAndHonourTheDocumentedOptOut()
	{
		using var secure = BuildProvider(services => services.AddIbmMqTransport(
			TransportName,
			mq =>
			{
				Configure(mq);
				mq.SslCipherSpec = "ANY_TLS12_OR_HIGHER";
			}));

		secure.GetRequiredKeyedService<IIbmMqConnectionProvider>(TransportName).ShouldNotBeNull();

		using var optedOut = BuildProvider(services => services.AddIbmMqTransport(
			TransportName,
			mq =>
			{
				Configure(mq);
				mq.RequireTls = false;
			}));

		optedOut.GetRequiredKeyedService<IIbmMqConnectionProvider>(TransportName).ShouldNotBeNull();
	}

	[Fact]
	public void RefuseACleartextGrpcServerAddress()
	{
		using var provider = BuildProvider(services => services.AddGrpcTransport(
			TransportName,
			grpc => grpc.ServerAddress = "http://localhost:5000"));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredKeyedService<GrpcChannel>(TransportName));

		refusal.TransportName.ShouldBe("gRPC");
		refusal.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
	}

	[Fact]
	public void AcceptAnHttpsGrpcServerAddressAndHonourTheDocumentedOptOut()
	{
		using var secure = BuildProvider(services => services.AddGrpcTransport(
			TransportName,
			grpc => grpc.ServerAddress = "https://localhost:5001"));

		secure.GetRequiredKeyedService<GrpcChannel>(TransportName).ShouldNotBeNull();

		using var optedOut = BuildProvider(services => services.AddGrpcTransport(
			TransportName,
			grpc =>
			{
				grpc.ServerAddress = "http://localhost:5000";
				grpc.RequireTls = false;
			}));

		optedOut.GetRequiredKeyedService<GrpcChannel>(TransportName).ShouldNotBeNull();
	}

	[Fact]
	public void RefuseAPlaintextPulsarServiceUrl()
	{
		using var provider = BuildProvider(services => services.AddPulsarTransport(
			TransportName,
			pulsar => pulsar.ServiceUrl("pulsar://localhost:6650").Topic("orders")));

		var refusal = Should.Throw<TransportSecurityException>(
			() => provider.GetRequiredKeyedService<DotPulsar.Abstractions.IPulsarClient>(TransportName));

		refusal.TransportName.ShouldBe("Pulsar");
		refusal.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
	}

	[Fact]
	public async Task AcceptAPulsarSslServiceUrlAndHonourTheDocumentedOptOut()
	{
		// A live PulsarClient is IAsyncDisposable only, so the container must be disposed asynchronously.
		await using var secure = BuildProvider(services => services.AddPulsarTransport(
			TransportName,
			pulsar => pulsar.ServiceUrl("pulsar+ssl://localhost:6651").Topic("orders")));

		secure.GetRequiredKeyedService<DotPulsar.Abstractions.IPulsarClient>(TransportName).ShouldNotBeNull();

		await using var optedOut = BuildProvider(services => services.AddPulsarTransport(
			TransportName,
			pulsar => pulsar.ServiceUrl("pulsar://localhost:6650").RequireTls(false).Topic("orders")));

		optedOut.GetRequiredKeyedService<DotPulsar.Abstractions.IPulsarClient>(TransportName).ShouldNotBeNull();
	}

	private static void Configure(MqttOptions options)
	{
		options.Host = "localhost";
		options.ClientId = "svc";
		options.Topic = "orders";
	}

	private static void Configure(IbmMqOptions options)
	{
		options.Host = "localhost";
		options.QueueManager = "QM1";
		options.Channel = "DEV.APP.SVRCONN";
		options.QueueName = "DEV.QUEUE.1";
	}

	private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<Serialization.IPayloadSerializer>());
		register(services);
		return services.BuildServiceProvider();
	}
}
