// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Excalibur.Dispatch.Transport.Kafka;

internal static class KafkaProducerConfigBuilder
{
	public static ProducerConfig Build(
			KafkaOptions options,
			KafkaCloudEventOptions? cloudEventOptions = null,
			KafkaMessageBusOptions? messageBusOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		var config = new ProducerConfig
		{
			BootstrapServers = options.BootstrapServers,
		};

		var ackLevel = messageBusOptions?.AckLevel
					   ?? cloudEventOptions?.AcknowledgmentLevel
					   ?? KafkaAckLevel.All;
		config.Acks = MapAcks(ackLevel);

		var compressionType = messageBusOptions?.CompressionType
							  ?? cloudEventOptions?.CompressionType
							  ?? KafkaCompressionType.None;
		var enableCompression = cloudEventOptions?.EnableCompression ?? false;
		config.CompressionType = enableCompression
				? MapCompressionType(compressionType)
				: CompressionType.None;

		if (cloudEventOptions is not null)
		{
			config.MessageMaxBytes = cloudEventOptions.MaxMessageSizeBytes;
			config.MessageSendMaxRetries = cloudEventOptions.RetrySettings.MaxRetries;
			config.Set(
					"retry.backoff.ms",
					((int)cloudEventOptions.RetrySettings.RetryDelay.TotalMilliseconds)
							.ToString());
			config.Set(
					"retry.backoff.max.ms",
					((int)cloudEventOptions.RetrySettings.MaxRetryDelay.TotalMilliseconds)
							.ToString());
		}

		var enableTransactions = cloudEventOptions?.EnableTransactions == true
								 || messageBusOptions?.EnableTransactions == true;
		var enableIdempotence = cloudEventOptions?.EnableIdempotentProducer == true || enableTransactions;

		if (enableIdempotence)
		{
			config.EnableIdempotence = true;
			config.Acks = Acks.All;
		}

		if (enableTransactions)
		{
			var transactionalId = messageBusOptions?.TransactionalId;
			if (string.IsNullOrWhiteSpace(transactionalId))
			{
				transactionalId = cloudEventOptions?.TransactionalId;
			}

			config.TransactionalId = string.IsNullOrWhiteSpace(transactionalId)
					? $"dispatch-{Guid.NewGuid():N}"
					: transactionalId;
		}

		foreach (var kvp in options.AdditionalConfig)
		{
			config.Set(kvp.Key, kvp.Value);
		}

		return config;
	}

	public static async Task EnsureTopicExistsAsync(
			KafkaOptions options,
			KafkaCloudEventOptions cloudEventOptions,
			string topic,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(cloudEventOptions);
		cancellationToken.ThrowIfCancellationRequested();

		if (!cloudEventOptions.AutoCreateTopics || string.IsNullOrWhiteSpace(topic))
		{
			return;
		}

		var adminConfig = new AdminClientConfig
		{
			BootstrapServers = options.BootstrapServers,
		};

		foreach (var kvp in options.AdditionalConfig)
		{
			adminConfig.Set(kvp.Key, kvp.Value);
		}

		using var admin = new AdminClientBuilder(adminConfig).Build();

		var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(10));
		if (metadata.Topics.Any(t => t.Topic == topic && t.Error.Code == ErrorCode.NoError))
		{
			return;
		}

		try
		{
			await admin
					.CreateTopicsAsync(
							[
									new global::Confluent.Kafka.Admin.TopicSpecification
									{
										Name = topic,
										NumPartitions = cloudEventOptions.DefaultPartitionCount,
										ReplicationFactor = cloudEventOptions.DefaultReplicationFactor,
									}
							],
							new CreateTopicsOptions
							{
								RequestTimeout = TimeSpan.FromSeconds(10),
								OperationTimeout = TimeSpan.FromSeconds(10),
							})
					.ConfigureAwait(false);
		}
		catch (CreateTopicsException ex) when (
				ex.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
		{
		}
	}

	private static Acks MapAcks(KafkaAckLevel level) =>
			level switch
			{
				KafkaAckLevel.None => Acks.None,
				KafkaAckLevel.Leader => Acks.Leader,
				_ => Acks.All,
			};

	private static CompressionType MapCompressionType(KafkaCompressionType type) =>
			type switch
			{
				KafkaCompressionType.Gzip => CompressionType.Gzip,
				KafkaCompressionType.Snappy => CompressionType.Snappy,
				KafkaCompressionType.Lz4 => CompressionType.Lz4,
				KafkaCompressionType.Zstd => CompressionType.Zstd,
				_ => CompressionType.None,
			};
}
