// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// <see cref="IMqttConnectionProvider"/> is public as a deliberate extension point: it is the supported
/// path for connection configuration that options cannot express, mutual TLS and private-CA validation
/// above all. These arms hold the registration to that promise.
/// </summary>
/// <remarks>
/// <para>
/// An extension point a consumer cannot actually reach is worse than none, because it is advertised. The
/// registration uses <c>TryAddKeyedSingleton</c>, whose whole purpose is to yield to a registration the
/// consumer made first — but "whose purpose is to" is a claim about intent, and the arms below are about
/// behaviour. If the framework's own registration silently won, the seam would be public, documented and
/// inert, and every consumer configuring client certificates through it would be quietly connecting with
/// the framework's default TLS posture instead.
/// </para>
/// <para>
/// <b>Resolution is not enough, so the second arm drives the transport.</b> Winning the container proves
/// a consumer can obtain their own provider; it does not prove the sender and receiver use it. Those hold
/// their client for their whole lifetime, so a provider consulted zero times looks identical to one
/// consulted correctly from outside.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MqttConnectionProviderSubstitutionShould
{
	private const string TransportName = "mqtt";

	/// <summary>
	/// SAFETY. A provider the consumer registered before calling <c>AddMqttTransport</c> is the one that
	/// resolves.
	/// </summary>
	[Fact]
	public async Task Resolve_the_provider_the_consumer_registered_first()
	{
		var consumerProvider = A.Fake<IMqttConnectionProvider>();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKeyedSingleton(TransportName, consumerProvider);
		_ = services.AddMqttTransport(TransportName, Configure);

		await using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredKeyedService<IMqttConnectionProvider>(TransportName);

		resolved.ShouldBeSameAs(consumerProvider, "the framework's own registration displaced the "
			+ "consumer's, so the extension point is advertised but unreachable: a consumer supplying "
			+ "client certificates through it would connect with the framework's default TLS posture and "
			+ "get no indication that their provider was ignored.");
	}

	/// <summary>
	/// LIVENESS. With no consumer registration the framework still supplies a working provider, so the
	/// arm above is not satisfied by a registration that registers nothing.
	/// </summary>
	[Fact]
	public async Task Supply_its_own_provider_when_the_consumer_registers_none()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMqttTransport(TransportName, Configure);

		await using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredKeyedService<IMqttConnectionProvider>(TransportName);

		resolved.ShouldNotBeNull();
		resolved.GetType().Name.ShouldBe("MqttConnectionProvider");
	}

	/// <summary>
	/// SAFETY, and the property that actually matters: the substituted provider is on the live connect
	/// path, not merely in the container.
	/// </summary>
	/// <remarks>
	/// Both the sender and the receiver take their client from the provider once and hold it, so a
	/// provider that is resolved and never consulted is indistinguishable from a working one by
	/// resolution alone. This drives a real receive through the registered receiver and asserts the
	/// consumer's provider supplied both the client and the connection options — the latter being where
	/// certificates and chain-validation callbacks live, and the reason the seam is public at all.
	/// </remarks>
	[Fact]
	public async Task Build_the_connection_from_the_consumers_provider()
	{
		var consumerProvider = A.Fake<IMqttConnectionProvider>();
		var consumerClient = A.Fake<IMqttClient>();
		_ = A.CallTo(() => consumerProvider.CreateClient()).Returns(consumerClient);
		_ = A.CallTo(() => consumerProvider.BuildClientOptions(A<string>._))
			.Returns(new MqttClientOptions { ClientId = "consumer-supplied" });

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKeyedSingleton(TransportName, consumerProvider);
		_ = services.AddMqttTransport(TransportName, Configure);

		await using var provider = services.BuildServiceProvider();
		var receiver = provider.GetRequiredKeyedService<ITransportReceiver>(TransportName);

		// ReceiveAsync connects and subscribes on the way in, then blocks on an empty buffer until its
		// token cancels. Nothing publishes here, so the cancellation is EXPECTED: the connect is what this
		// arm is here for. An unbounded token would hang the run rather than fail it.
		using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		try
		{
			_ = await receiver.ReceiveAsync(maxMessages: 1, bounded.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected — see above.
		}

		A.CallTo(() => consumerProvider.CreateClient()).MustHaveHappened();
		A.CallTo(() => consumerProvider.BuildClientOptions(A<string>._)).MustHaveHappened();
		A.CallTo(() => consumerClient.ConnectAsync(
			A<MqttClientOptions>.That.Matches(o => o.ClientId == "consumer-supplied"),
			A<CancellationToken>._)).MustHaveHappened();
	}

	private static void Configure(MqttOptions options)
	{
		options.Host = "broker.example.com";
		options.ClientId = "framework-default";
		options.Topic = "orders/topic";
		options.RequireTls = false;
	}
}
