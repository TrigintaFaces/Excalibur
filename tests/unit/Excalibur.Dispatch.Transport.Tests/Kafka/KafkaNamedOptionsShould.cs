// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Locks that a Kafka transport's runtime options belong to the transport that configured them.
/// </summary>
/// <remarks>
/// The transport is registered under a name while its runtime options were registered without one, so two
/// named Kafka transports in one container wrote the same options instance and the second silently
/// replaced the first. Nothing threw and nothing logged: the losing transport simply ran on the winner's
/// brokers and consumer group. The correct shape already shipped three files away in the MQTT, Pulsar and
/// IBM MQ transports, and in Azure Service Bus.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class KafkaNamedOptionsShould
{
	private static KafkaOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get(name);

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddKafkaTransport("orders", kafka => kafka
			.BootstrapServers("orders-broker:9092")
			.ConfigureConsumer(consumer => consumer.GroupId("orders-group").MaxBatchSize(11)));

		_ = services.AddKafkaTransport("audit", kafka => kafka
			.BootstrapServers("audit-broker:9092")
			.ConfigureConsumer(consumer => consumer.GroupId("audit-group").MaxBatchSize(22)));

		using var provider = services.BuildServiceProvider();

		// Pre-fix both of these read the second registration's values, because both Configure delegates
		// wrote the one unnamed instance.
		Resolve(provider, "orders").BootstrapServers.ShouldBe("orders-broker:9092");
		Resolve(provider, "orders").ConsumerGroup.ShouldBe("orders-group");
		Resolve(provider, "orders").Consumer.MaxBatchSize.ShouldBe(11);

		Resolve(provider, "audit").BootstrapServers.ShouldBe("audit-broker:9092");
		Resolve(provider, "audit").ConsumerGroup.ShouldBe("audit-group");
		Resolve(provider, "audit").Consumer.MaxBatchSize.ShouldBe(22);
	}

	[Fact]
	public void CarryFluentlyConfiguredValuesToTheResolvedNamedOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddKafkaTransport("orders", kafka => kafka
			.BootstrapServers("broker:9092")
			.ConfigureConsumer(consumer => consumer
				.GroupId("orders-group")
				.AutoOffsetReset(KafkaOffsetReset.Earliest)
				.EnableAutoCommit(false)));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "orders");

		options.BootstrapServers.ShouldBe("broker:9092");
		options.ConsumerGroup.ShouldBe("orders-group");
		options.Consumer.AutoOffsetReset.ShouldBe("earliest");
		options.Consumer.EnableAutoCommit.ShouldBeFalse();
	}

	[Fact]
	public void StillConfigureTheUnnamedOptionsForASingleTransportHost()
	{
		// The liveness arm, and the one a fix could most easily break: several types in this package take
		// IOptions<KafkaOptions> in their constructors, which resolves the UNNAMED instance. Naming the
		// options and stopping there would leave those resolving an empty object — a silent failure worse
		// than the overwrite being fixed, and one no assertion about named options can detect.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddKafkaTransport(kafka => kafka
			.BootstrapServers("broker:9092")
			.ConfigureConsumer(consumer => consumer.GroupId("single-group")));

		using var provider = services.BuildServiceProvider();

		var unnamed = provider.GetRequiredService<IOptions<KafkaOptions>>().Value;
		unnamed.BootstrapServers.ShouldBe("broker:9092");
		unnamed.ConsumerGroup.ShouldBe("single-group");

		// And the default name resolves the same configuration, so a host that reaches the options either
		// way sees one answer.
		Resolve(provider, "kafka").BootstrapServers.ShouldBe("broker:9092");
	}
}
