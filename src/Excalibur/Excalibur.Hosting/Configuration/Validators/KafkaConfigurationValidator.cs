// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates Apache Kafka configuration settings.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="KafkaConfigurationValidator" /> class. </remarks>
/// <param name="configSection"> The configuration section to validate. </param>
public sealed class KafkaConfigurationValidator(string configSection = "Kafka") : MessageBrokerValidator($"Kafka:{configSection}")
{
	/// <inheritdoc />
	public override Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();
		var kafkaConfig = configuration.GetSection(configSection);

		// Validate bootstrap servers
		var bootstrapServers = kafkaConfig["BootstrapServers"];
		if (string.IsNullOrWhiteSpace(bootstrapServers))
		{
			errors.Add(new ConfigurationValidationError(
				"Kafka bootstrap servers are missing",
				$"{configSection}:BootstrapServers",
				value: null,
				"Provide comma-separated list of Kafka brokers (e.g., localhost:9092,localhost:9093)"));
		}
		else
		{
			// Validate each bootstrap server
			foreach (var server in bootstrapServers.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				var trimmedServer = server.Trim();
				if (!trimmedServer.Contains(':', StringComparison.Ordinal))
				{
					errors.Add(new ConfigurationValidationError(
						$"Invalid Kafka bootstrap server format: {trimmedServer}",
						$"{configSection}:BootstrapServers",
						trimmedServer,
						"Use format 'host:port' for each server"));
				}
				else
				{
					var parts = trimmedServer.Split(':');
					if (parts.Length != 2 || !int.TryParse(parts[1], out var port) || port < 1 || port > 65535)
					{
						errors.Add(new ConfigurationValidationError(
							$"Invalid port in Kafka bootstrap server: {trimmedServer}",
							$"{configSection}:BootstrapServers",
							trimmedServer,
							"Use a valid port number (1-65535)"));
					}
				}
			}
		}

		// Validate consumer configuration
		var consumerConfig = kafkaConfig.GetSection("Consumer");
		if (consumerConfig.Exists())
		{
			ValidateConsumerConfiguration(consumerConfig, errors);
		}

		// Validate producer configuration
		var producerConfig = kafkaConfig.GetSection("Producer");
		if (producerConfig.Exists())
		{
			ValidateProducerConfiguration(producerConfig, errors);
		}

		// Validate security settings
		var securityProtocol = kafkaConfig["SecurityProtocol"];
		if (!string.IsNullOrWhiteSpace(securityProtocol))
		{
			var validProtocols = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PLAINTEXT", "SSL", "SASL_PLAINTEXT", "SASL_SSL" };

			_ = ValidateEnum(
				kafkaConfig,
				"SecurityProtocol",
				validProtocols,
				errors,
				StringComparison.OrdinalIgnoreCase,
				"PLAINTEXT");

			// Validate SASL settings if using SASL
			if (securityProtocol.Contains("SASL", StringComparison.OrdinalIgnoreCase))
			{
				ValidateSaslConfiguration(kafkaConfig, errors);
			}

			// Validate SSL settings if using SSL
			if (securityProtocol.Contains("SSL", StringComparison.OrdinalIgnoreCase))
			{
				ValidateSslConfiguration(kafkaConfig, errors);
			}
		}

		return Task.FromResult(errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors));
	}

	private static void ValidateProducerConfiguration(IConfigurationSection producerConfig, List<ConfigurationValidationError> errors)
	{
		// Validate acknowledgments
		var acks = producerConfig["Acks"];
		if (!string.IsNullOrWhiteSpace(acks))
		{
			var validValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "0", "1", "-1", "all" };

			_ = ValidateEnum(
				producerConfig,
				"Acks",
				validValues,
				errors,
				StringComparison.OrdinalIgnoreCase,
				"1");
		}

		// Validate compression type
		var compressionType = producerConfig["CompressionType"];
		if (!string.IsNullOrWhiteSpace(compressionType))
		{
			var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"none",
				"gzip",
				"snappy",
				"lz4",
				"zstd",
			};

			_ = ValidateEnum(
				producerConfig,
				"CompressionType",
				validTypes,
				errors,
				StringComparison.OrdinalIgnoreCase,
				"none");
		}

		// Validate batch size
		var batchSize = producerConfig["BatchSize"];
		if (!string.IsNullOrWhiteSpace(batchSize))
		{
			_ = ValidateIntRange(
				producerConfig,
				"BatchSize",
				0,
				int.MaxValue,
				errors,
				16384);
		}

		// Validate linger time
		var lingerMs = producerConfig["LingerMs"];
		if (!string.IsNullOrWhiteSpace(lingerMs))
		{
			_ = ValidateIntRange(
				producerConfig,
				"LingerMs",
				0,
				int.MaxValue,
				errors,
				0);
		}

		// Validate retries
		var retries = producerConfig["Retries"];
		if (!string.IsNullOrWhiteSpace(retries))
		{
			_ = ValidateIntRange(
				producerConfig,
				"Retries",
				0,
				int.MaxValue,
				errors,
				2147483647);
		}
	}

	private void ValidateConsumerConfiguration(IConfigurationSection consumerConfig, List<ConfigurationValidationError> errors)
	{
		var groupId = consumerConfig["GroupId"];
		if (string.IsNullOrWhiteSpace(groupId))
		{
			errors.Add(new ConfigurationValidationError(
				"Kafka consumer group ID is missing",
				$"{configSection}:Consumer:GroupId",
				value: null,
				"Provide a consumer group ID for message consumption"));
		}

		// Validate auto offset reset
		var autoOffsetReset = consumerConfig["AutoOffsetReset"];
		if (!string.IsNullOrWhiteSpace(autoOffsetReset))
		{
			var validValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "earliest", "latest", "none" };

			_ = ValidateEnum(
				consumerConfig,
				"AutoOffsetReset",
				validValues,
				errors,
				StringComparison.OrdinalIgnoreCase,
				"latest");
		}

		// Validate session timeout
		var sessionTimeout = consumerConfig["SessionTimeoutMs"];
		if (!string.IsNullOrWhiteSpace(sessionTimeout))
		{
			_ = ValidateIntRange(
				consumerConfig,
				"SessionTimeoutMs",
				6000,
				300000,
				errors,
				10000);
		}

		// Validate max poll records
		var maxPollRecords = consumerConfig["MaxPollRecords"];
		if (!string.IsNullOrWhiteSpace(maxPollRecords))
		{
			_ = ValidateIntRange(
				consumerConfig,
				"MaxPollRecords",
				1,
				10000,
				errors,
				500);
		}
	}

	private void ValidateSaslConfiguration(IConfigurationSection kafkaConfig, List<ConfigurationValidationError> errors)
	{
		var saslMechanism = kafkaConfig["SaslMechanism"];
		if (string.IsNullOrWhiteSpace(saslMechanism))
		{
			errors.Add(new ConfigurationValidationError(
				"SASL mechanism is required when using SASL security",
				$"{configSection}:SaslMechanism",
				value: null,
				"Set to PLAIN, SCRAM-SHA-256, SCRAM-SHA-512, or OAUTHBEARER"));
		}
		else
		{
			var validMechanisms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"PLAIN", "SCRAM-SHA-256", "SCRAM-SHA-512", "OAUTHBEARER",
			};

			_ = ValidateEnum(
				kafkaConfig,
				"SaslMechanism",
				validMechanisms,
				errors);
		}

		var saslUsername = kafkaConfig["SaslUsername"];
		var saslPassword = kafkaConfig["SaslPassword"];

		if (string.IsNullOrWhiteSpace(saslUsername))
		{
			errors.Add(new ConfigurationValidationError(
				"SASL username is required when using SASL security",
				$"{configSection}:SaslUsername",
				value: null,
				"Provide the SASL username"));
		}

		if (string.IsNullOrWhiteSpace(saslPassword))
		{
			errors.Add(new ConfigurationValidationError(
				"SASL password is required when using SASL security",
				$"{configSection}:SaslPassword",
				value: null,
				"Provide the SASL password"));
		}
	}

	private void ValidateSslConfiguration(IConfigurationSection kafkaConfig, List<ConfigurationValidationError> errors)
	{
		var sslCaLocation = kafkaConfig["SslCaLocation"];
		var sslCertificateLocation = kafkaConfig["SslCertificateLocation"];
		var sslKeyLocation = kafkaConfig["SslKeyLocation"];

		// Validate CA certificate
		if (!string.IsNullOrWhiteSpace(sslCaLocation) && !File.Exists(sslCaLocation))
		{
			errors.Add(new ConfigurationValidationError(
				"SSL CA certificate file not found",
				$"{configSection}:SslCaLocation",
				sslCaLocation,
				"Ensure the CA certificate file exists at the specified path"));
		}

		// Validate client certificate
		if (!string.IsNullOrWhiteSpace(sslCertificateLocation) && !File.Exists(sslCertificateLocation))
		{
			errors.Add(new ConfigurationValidationError(
				"SSL client certificate file not found",
				$"{configSection}:SslCertificateLocation",
				sslCertificateLocation,
				"Ensure the client certificate file exists at the specified path"));
		}

		// Validate client key
		if (!string.IsNullOrWhiteSpace(sslKeyLocation) && !File.Exists(sslKeyLocation))
		{
			errors.Add(new ConfigurationValidationError(
				"SSL client key file not found",
				$"{configSection}:SslKeyLocation",
				sslKeyLocation,
				"Ensure the client key file exists at the specified path"));
		}

		// Ensure certificate and key are both present or both absent
		if (!string.IsNullOrWhiteSpace(sslCertificateLocation) && string.IsNullOrWhiteSpace(sslKeyLocation))
		{
			errors.Add(new ConfigurationValidationError(
				"SSL client key is required when certificate is provided",
				$"{configSection}:SslKeyLocation",
				value: null,
				"Provide the SSL client key file path"));
		}

		if (string.IsNullOrWhiteSpace(sslCertificateLocation) && !string.IsNullOrWhiteSpace(sslKeyLocation))
		{
			errors.Add(new ConfigurationValidationError(
				"SSL client certificate is required when key is provided",
				$"{configSection}:SslCertificateLocation",
				value: null,
				"Provide the SSL client certificate file path"));
		}
	}
}
