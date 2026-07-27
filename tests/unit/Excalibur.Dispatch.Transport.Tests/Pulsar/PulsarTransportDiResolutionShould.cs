// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>
/// DI-resolution regression lock proving that <c>AddPulsarTransport(name, …)</c> registers a keyed
/// <see cref="IMessageBus"/> (the full dispatch publisher) that resolves from a real container, coexisting
/// with the keyed <see cref="ITransportSender"/>/<see cref="ITransportReceiver"/> primitives. Mirrors
/// <c>GrpcTransportDiResolutionShould</c>. RED if the keyed <see cref="IMessageBus"/> registration is
/// missing (advertised-but-unwired).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PulsarTransportDiResolutionShould
{
	private const string TransportName = "events";

	[Fact]
	public async Task ResolveKeyedMessageBusFromRegistration()
	{
		await using var provider = BuildProvider();

		var bus = provider.GetRequiredKeyedService<IMessageBus>(TransportName);

		bus.ShouldNotBeNull();
	}

	[Fact]
	public async Task ResolveKeyedTransportSenderAlongsideMessageBus()
	{
		await using var provider = BuildProvider();

		provider.GetRequiredKeyedService<ITransportSender>(TransportName).ShouldNotBeNull();
		provider.GetRequiredKeyedService<IMessageBus>(TransportName).ShouldNotBeNull();
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		// The message bus factory resolves IPayloadSerializer; provide a fake so the unit test does not
		// depend on the full dispatch core registration.
		services.AddSingleton(A.Fake<IPayloadSerializer>());
		services.AddPulsarTransport(TransportName, pulsar =>
			pulsar.ServiceUrl("pulsar://localhost:6650").Topic("orders"));
		return services.BuildServiceProvider();
	}
}
