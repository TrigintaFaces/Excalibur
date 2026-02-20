// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates RabbitMQ configuration settings.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RabbitMqConfigurationValidator" /> class. </remarks>
/// <param name="configSection"> The configuration section to validate. </param>
public sealed class RabbitMqConfigurationValidator(string configSection = "RabbitMQ") : MessageBrokerValidator($"RabbitMQ:{configSection}")
{
	/// <inheritdoc />
	public override Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();
		var rabbitConfig = configuration.GetSection(configSection);

		// Validate connection settings
		var host = rabbitConfig["Host"] ?? rabbitConfig["HostName"];
		var port = rabbitConfig["Port"];
		var virtualHost = rabbitConfig["VirtualHost"];
		var username = rabbitConfig["Username"] ?? rabbitConfig["UserName"];
		var password = rabbitConfig["Password"];
		var connectionString = rabbitConfig["ConnectionString"];

		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			// Validate connection string format
			_ = ValidateEndpoint(connectionString, errors, $"{configSection}:ConnectionString", "amqp");
		}
		else
		{
			// Validate individual connection parameters
			if (string.IsNullOrWhiteSpace(host))
			{
				errors.Add(new ConfigurationValidationError(
					"RabbitMQ host is missing",
					$"{configSection}:Host",
					value: null,
					"Provide the RabbitMQ server hostname or IP address"));
			}

			// Validate port if specified
			if (!string.IsNullOrWhiteSpace(port))
			{
				_ = ValidateIntRange(
					rabbitConfig,
					"Port",
					1,
					65535,
					errors,
					5672); // Default RabbitMQ port
			}

			// Validate virtual host
			if (!string.IsNullOrWhiteSpace(virtualHost) && virtualHost.Contains(' ', StringComparison.Ordinal))
			{
				errors.Add(new ConfigurationValidationError(
					"RabbitMQ virtual host cannot contain spaces",
					$"{configSection}:VirtualHost",
					virtualHost,
					"Use a valid virtual host name without spaces"));
			}

			// Validate credentials
			if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
			{
				errors.Add(new ConfigurationValidationError(
					"RabbitMQ password is required when username is specified",
					$"{configSection}:Password",
					value: null,
					"Provide the password for the RabbitMQ user"));
			}
		}

		// Validate exchange settings if present
		var exchangeConfig = rabbitConfig.GetSection("Exchange");
		if (exchangeConfig.Exists())
		{
			ValidateExchangeConfiguration(exchangeConfig, errors);
		}

		// Validate queue settings if present
		var queueConfig = rabbitConfig.GetSection("Queue");
		if (queueConfig.Exists())
		{
			ValidateQueueConfiguration(queueConfig, errors);
		}

		// Validate connection pool settings
		var maxConnections = rabbitConfig["MaxConnections"];
		if (!string.IsNullOrWhiteSpace(maxConnections))
		{
			_ = ValidateIntRange(
				rabbitConfig,
				"MaxConnections",
				1,
				1000,
				errors);
		}

		// Validate heartbeat interval
		var heartbeat = rabbitConfig["Heartbeat"];
		if (!string.IsNullOrWhiteSpace(heartbeat))
		{
			_ = ValidateTimeSpan(
				rabbitConfig,
				"Heartbeat",
				TimeSpan.FromSeconds(5),
				TimeSpan.FromMinutes(10),
				errors,
				TimeSpan.FromSeconds(60));
		}

		return Task.FromResult(errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors));
	}

	private void ValidateExchangeConfiguration(IConfigurationSection exchangeConfig, List<ConfigurationValidationError> errors)
	{
		var exchangeName = exchangeConfig["Name"];
		var exchangeType = exchangeConfig["Type"];

		if (!string.IsNullOrWhiteSpace(exchangeName))
		{
			// RabbitMQ exchange naming rules
			if (exchangeName.StartsWith("amq.", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Exchange names starting with 'amq.' are reserved",
					$"{configSection}:Exchange:Name",
					exchangeName,
					"Use a different exchange name"));
			}
		}

		if (!string.IsNullOrWhiteSpace(exchangeType))
		{
			var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "direct", "topic", "fanout", "headers" };

			_ = ValidateEnum(
				exchangeConfig,
				"Type",
				validTypes,
				errors,
				StringComparison.OrdinalIgnoreCase,
				"direct");
		}

		// Validate durability setting
		var durable = exchangeConfig["Durable"];
		if (!string.IsNullOrWhiteSpace(durable) && !bool.TryParse(durable, out _))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid durability setting",
				$"{configSection}:Exchange:Durable",
				durable,
				"Set to 'true' or 'false'"));
		}
	}

	private void ValidateQueueConfiguration(IConfigurationSection queueConfig, List<ConfigurationValidationError> errors)
	{
		var queueName = queueConfig["Name"];

		if (!string.IsNullOrWhiteSpace(queueName))
		{
			// RabbitMQ queue naming rules
			if (queueName.StartsWith("amq.", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add(new ConfigurationValidationError(
					"Queue names starting with 'amq.' are reserved",
					$"{configSection}:Queue:Name",
					queueName,
					"Use a different queue name"));
			}

			if (queueName.Length > 255)
			{
				errors.Add(new ConfigurationValidationError(
					"Queue name exceeds maximum length of 255 characters",
					$"{configSection}:Queue:Name",
					queueName,
					"Use a shorter queue name"));
			}
		}

		// Validate TTL settings
		var messageTtl = queueConfig["MessageTtl"];
		if (!string.IsNullOrWhiteSpace(messageTtl))
		{
			if (int.TryParse(messageTtl, out var ttl))
			{
				if (ttl < 0)
				{
					errors.Add(new ConfigurationValidationError(
						"Message TTL cannot be negative",
						$"{configSection}:Queue:MessageTtl",
						ttl,
						"Set to 0 for no TTL or a positive value in milliseconds"));
				}
			}
			else
			{
				errors.Add(new ConfigurationValidationError(
					"Invalid message TTL format",
					$"{configSection}:Queue:MessageTtl",
					messageTtl,
					"Set to a valid integer representing milliseconds"));
			}
		}

		// Validate max length
		var maxLength = queueConfig["MaxLength"];
		if (!string.IsNullOrWhiteSpace(maxLength))
		{
			_ = ValidateIntRange(
				queueConfig,
				"MaxLength",
				1,
				int.MaxValue,
				errors);
		}
	}
}
