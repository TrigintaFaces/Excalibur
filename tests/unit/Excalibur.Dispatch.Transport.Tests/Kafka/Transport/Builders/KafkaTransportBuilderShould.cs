// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IKafkaTransportBuilder"/> and related builders.
/// Part of S472.2 - AddKafkaTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class KafkaTransportBuilderShould : UnitTestBase
{
	private const string ValidBootstrapServers = "localhost:9092";

	#region AddKafkaTransport Entry Point Tests

	[Fact]
	public void AddKafkaTransport_ThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddKafkaTransport("kafka", kafka =>
			{
				_ = kafka.BootstrapServers(ValidBootstrapServers);
			}));
	}

	[Fact]
	public void AddKafkaTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddKafkaTransport(null!, kafka =>
			{
				_ = kafka.BootstrapServers(ValidBootstrapServers);
			}));
	}

	[Fact]
	public void AddKafkaTransport_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddKafkaTransport("", kafka =>
			{
				_ = kafka.BootstrapServers(ValidBootstrapServers);
			}));
	}

	[Fact]
	public void AddKafkaTransport_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddKafkaTransport("kafka", null!));
	}

	[Fact]
	public void AddKafkaTransport_InvokeConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureInvoked = false;

		// Act
		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			configureInvoked = true;
			_ = kafka.BootstrapServers(ValidBootstrapServers);
		});

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void AddKafkaTransport_UseDefaultNameWhenNotSpecified()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddKafkaTransport(kafka =>
			{
				_ = kafka.BootstrapServers(ValidBootstrapServers);
			});
		});
	}

	#endregion

	#region BootstrapServers Tests

	[Fact]
	public void BootstrapServers_ThrowWhenServersIsNull()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.BootstrapServers(null!));
	}

	[Fact]
	public void BootstrapServers_ThrowWhenServersIsEmpty()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.BootstrapServers(""));
	}

	[Fact]
	public void BootstrapServers_SetServersInOptions()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.BootstrapServers("broker1:9092,broker2:9092");

		// Assert
		options.BootstrapServers.ShouldBe("broker1:9092,broker2:9092");
	}

	[Fact]
	public void BootstrapServers_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.BootstrapServers(ValidBootstrapServers);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseSchemaRegistry Tests

	[Fact]
	public void UseSchemaRegistry_EnableSchemaRegistryWithDefaults()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.UseSchemaRegistry();

		// Assert
		options.UseSchemaRegistryEnabled.ShouldBeTrue();
		_ = options.SchemaRegistry.ShouldNotBeNull();
	}

	[Fact]
	public void UseSchemaRegistry_InvokeConfigureCallback()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.UseSchemaRegistry(registry =>
		{
			registry.Url = "http://schema-registry:8081";
		});

		// Assert
		options.SchemaRegistry.Url.ShouldBe("http://schema-registry:8081");
	}

	[Fact]
	public void UseSchemaRegistry_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.UseSchemaRegistry();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureProducer Tests

	[Fact]
	public void ConfigureProducer_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureProducer(null!));
	}

	[Fact]
	public void ConfigureProducer_InvokeConfigureCallback()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.ConfigureProducer(producer =>
		{
			_ = producer.Acks(KafkaAckLevel.Leader);
		});

		// Assert
		_ = options.ProducerOptions.ShouldNotBeNull();
		options.ProducerOptions.Acks.ShouldBe(KafkaAckLevel.Leader);
	}

	[Fact]
	public void ConfigureProducer_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.ConfigureProducer(producer => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureConsumer Tests

	[Fact]
	public void ConfigureConsumer_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureConsumer(null!));
	}

	[Fact]
	public void ConfigureConsumer_InvokeConfigureCallback()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.ConfigureConsumer(consumer =>
		{
			_ = consumer.GroupId("my-group");
		});

		// Assert
		_ = options.ConsumerOptions.ShouldNotBeNull();
		options.ConsumerOptions.GroupId.ShouldBe("my-group");
	}

	[Fact]
	public void ConfigureConsumer_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.ConfigureConsumer(consumer => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MapTopic Tests

	[Fact]
	public void MapTopic_ThrowWhenTopicIsNull()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapTopic<TestMessage>(null!));
	}

	[Fact]
	public void MapTopic_ThrowWhenTopicIsEmpty()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapTopic<TestMessage>(""));
	}

	[Fact]
	public void MapTopic_AddMappingToOptions()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.MapTopic<TestMessage>("test-topic");

		// Assert
		options.TopicMappings.ShouldContainKey(typeof(TestMessage));
		options.TopicMappings[typeof(TestMessage)].ShouldBe("test-topic");
	}

	[Fact]
	public void MapTopic_SupportMultipleMappings()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.MapTopic<TestMessage>("test-topic")
			   .MapTopic<AnotherMessage>("another-topic");

		// Assert
		options.TopicMappings.Count.ShouldBe(2);
		options.HasTopicMappings.ShouldBeTrue();
	}

	[Fact]
	public void MapTopic_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.MapTopic<TestMessage>("test-topic");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithTopicPrefix Tests

	[Fact]
	public void WithTopicPrefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithTopicPrefix(null!));
	}

	[Fact]
	public void WithTopicPrefix_SetPrefixInOptions()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		_ = builder.WithTopicPrefix("myapp-prod-");

		// Assert
		options.TopicPrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void WithTopicPrefix_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new KafkaTransportOptions();
		var builder = new KafkaTransportBuilder(options);

		// Act
		var result = builder.WithTopicPrefix("myapp-");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void AddKafkaTransport_SupportFullFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddKafkaTransport("events", kafka =>
			{
				_ = kafka.BootstrapServers("broker1:9092,broker2:9092")
					 .UseSchemaRegistry(registry =>
					 {
						 registry.Url = "http://localhost:8081";
						 registry.DefaultCompatibility = CompatibilityMode.Backward;
					 })
					 .ConfigureProducer(producer =>
					 {
						 _ = producer.Acks(KafkaAckLevel.All)
								 .EnableIdempotence(true)
								 .CompressionType(KafkaCompressionType.Snappy)
								 .LingerMs(TimeSpan.FromMilliseconds(10));
					 })
					 .ConfigureConsumer(consumer =>
					 {
						 _ = consumer.GroupId("my-consumer-group")
								 .AutoOffsetReset(KafkaOffsetReset.Earliest)
								 .SessionTimeout(TimeSpan.FromSeconds(45))
								 .MaxBatchSize(100);
					 })
					 .MapTopic<TestMessage>("test-topic")
					 .WithTopicPrefix("myapp-");
			});
		});
	}

	[Fact]
	public void AddKafkaTransport_ConfigureKafkaOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			_ = kafka.BootstrapServers("broker:9092")
				 .ConfigureConsumer(consumer =>
				 {
					 _ = consumer.GroupId("test-group")
							 .AutoOffsetReset(KafkaOffsetReset.Earliest);
				 })
				 .ConfigureProducer(producer =>
				 {
					 _ = producer.ClientId("test-producer")
							 .Acks(KafkaAckLevel.Leader);
				 });
		});

		using var provider = services.BuildServiceProvider();
		var kafkaOptions = provider.GetRequiredService<IOptions<KafkaOptions>>().Value;

		// Assert
		kafkaOptions.BootstrapServers.ShouldBe("broker:9092");
		kafkaOptions.ConsumerGroup.ShouldBe("test-group");
		kafkaOptions.AutoOffsetReset.ShouldBe("earliest");
		kafkaOptions.AdditionalConfig.ShouldContainKey("client.id");
		kafkaOptions.AdditionalConfig["client.id"].ShouldBe("test-producer");
	}

	[Fact]
	public void AddKafkaTransport_ConfigureCloudEventOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			_ = kafka.BootstrapServers("broker:9092")
				 .ConfigureProducer(producer =>
				 {
					 _ = producer.Acks(KafkaAckLevel.Leader)
							 .EnableIdempotence(true)
							 .CompressionType(KafkaCompressionType.Gzip)
							 .EnableTransactions("txn-id");
				 })
				 .ConfigureConsumer(consumer =>
				 {
					 _ = consumer.GroupId("test-group")
							 .AutoOffsetReset(KafkaOffsetReset.Earliest);
				 });
		});

		using var provider = services.BuildServiceProvider();
		var cloudEventOptions = provider.GetRequiredService<IOptions<KafkaCloudEventOptions>>().Value;

		// Assert
		cloudEventOptions.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.Leader);
		cloudEventOptions.EnableIdempotentProducer.ShouldBeTrue();
		cloudEventOptions.CompressionType.ShouldBe(KafkaCompressionType.Gzip);
		cloudEventOptions.EnableTransactions.ShouldBeTrue();
		cloudEventOptions.TransactionalId.ShouldBe("txn-id");
		cloudEventOptions.ConsumerGroupId.ShouldBe("test-group");
		cloudEventOptions.OffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
	}

	#endregion

	#region Helper Classes

	private sealed class TestMessage { }
	private sealed class AnotherMessage { }

	#endregion
}
