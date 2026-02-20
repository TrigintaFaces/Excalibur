// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Configuration;

/// <summary>
/// Unit tests for KafkaTransportServiceCollectionExtensions option wiring.
/// Updated for ADR-098 compliant AddKafkaTransport() API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class KafkaServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddKafkaTransport_MapsOptionsToKafkaOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			_ = kafka.BootstrapServers("broker:9092")
				 .ConfigureConsumer(consumer =>
				 {
					 _ = consumer.GroupId("dispatch-group");
				 })
				 .ConfigureProducer(producer =>
				 {
					 _ = producer.ClientId("dispatch-producer");
				 });
		});

		using var provider = services.BuildServiceProvider();
		var kafkaOptions = provider.GetRequiredService<IOptions<KafkaOptions>>().Value;

		kafkaOptions.BootstrapServers.ShouldBe("broker:9092");
		kafkaOptions.ConsumerGroup.ShouldBe("dispatch-group");
		kafkaOptions.AdditionalConfig.ShouldContainKey("client.id");
		kafkaOptions.AdditionalConfig["client.id"].ShouldBe("dispatch-producer");
	}

	[Fact]
	public void AddKafkaTransport_MapsOptionsToCloudEventOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			_ = kafka.BootstrapServers("broker:9092")
				 .ConfigureProducer(producer =>
				 {
					 _ = producer.EnableTransactions("dispatch-txn")
							 .CompressionType(KafkaCompressionType.Gzip)
							 .Acks(KafkaAckLevel.Leader);
				 })
				 .ConfigureConsumer(consumer =>
				 {
					 _ = consumer.GroupId("dispatch-group")
							 .AutoOffsetReset(KafkaOffsetReset.Earliest);
				 });
		});

		using var provider = services.BuildServiceProvider();
		var cloudEventOptions = provider.GetRequiredService<IOptions<KafkaCloudEventOptions>>().Value;

		cloudEventOptions.EnableTransactions.ShouldBeTrue();
		cloudEventOptions.TransactionalId.ShouldBe("dispatch-txn");
		cloudEventOptions.CompressionType.ShouldBe(KafkaCompressionType.Gzip);
		cloudEventOptions.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.Leader);
		cloudEventOptions.ConsumerGroupId.ShouldBe("dispatch-group");
		cloudEventOptions.OffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
	}
}
