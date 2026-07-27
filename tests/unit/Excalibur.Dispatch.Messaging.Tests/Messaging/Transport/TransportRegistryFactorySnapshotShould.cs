// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TransportRegistryFactorySnapshotShould
{
	[Fact]
	public void IncludePendingFactoryNames_InTransportNamesSnapshot()
	{
		var registry = new TransportRegistry();
		registry.RegisterTransportFactory(
			"factory-rabbit",
			"RabbitMQ",
			TransportLocality.Remote,
			static _ => A.Fake<ITransportAdapter>());

		var names = registry.GetTransportNames().ToList();

		names.Count.ShouldBe(1);
		names.ShouldContain("factory-rabbit");
	}

	[Fact]
	public void KeepSingleName_WhenFactoryAndTransportShareNameAfterInitialization()
	{
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("rabbit");

		registry.RegisterTransportFactory("rabbit", "RabbitMQ", TransportLocality.Remote, _ => adapter);
		registry.InitializeFactories(A.Fake<IServiceProvider>());

		var names = registry.GetTransportNames().ToList();

		names.Count.ShouldBe(1);
		names[0].ShouldBe("rabbit");
	}

	[Fact]
	public void RefreshSnapshot_WhenTransportIsRemoved()
	{
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbit", CreateAdapter("rabbit"), "RabbitMQ", TransportLocality.Remote);
		registry.RegisterTransport("kafka", CreateAdapter("kafka"), "Kafka", TransportLocality.Remote);

		registry.RemoveTransport("kafka").ShouldBeTrue();

		var names = registry.GetTransportNames().ToList();
		names.Count.ShouldBe(1);
		names[0].ShouldBe("rabbit");
	}

	[Fact]
	public void KeepFactoryNames_WhenClearRemovesRegisteredTransports()
	{
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbit", CreateAdapter("rabbit"), "RabbitMQ", TransportLocality.Remote);
		registry.RegisterTransportFactory(
			"factory-kafka",
			"Kafka",
			TransportLocality.Remote,
			static _ => A.Fake<ITransportAdapter>());

		registry.Clear();

		var names = registry.GetTransportNames().ToList();
		names.Count.ShouldBe(1);
		names[0].ShouldBe("factory-kafka");
	}

	private static ITransportAdapter CreateAdapter(string name)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.TransportType).Returns("Test");
		_ = A.CallTo(() => adapter.IsRunning).Returns(true);
		return adapter;
	}
}