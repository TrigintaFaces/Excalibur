// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Message mapper that applies configured mappings from the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// This mapper applies type-specific and default mappings configured through
/// <see cref="IMessageMappingBuilder"/>. It checks for type-specific configurations
/// first, then falls back to default configurations if available.
/// </para>
/// </remarks>
public sealed class ConfiguredMessageMapper : IMessageMapper
{
	private readonly ConcurrentDictionary<Type, MessageTypeMapperConfiguration> _typeConfigurations;
	private readonly DefaultMappingConfiguration _defaultConfiguration;
	private readonly IMessageMapperRegistry _registry;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfiguredMessageMapper"/> class.
	/// </summary>
	/// <param name="typeConfigurations">The type-specific configurations.</param>
	/// <param name="defaultConfiguration">The default mapping configuration.</param>
	/// <param name="registry">The mapper registry for fallback lookups.</param>
	internal ConfiguredMessageMapper(
		ConcurrentDictionary<Type, MessageTypeMapperConfiguration> typeConfigurations,
		DefaultMappingConfiguration defaultConfiguration,
		IMessageMapperRegistry registry)
	{
		_typeConfigurations = typeConfigurations ?? throw new ArgumentNullException(nameof(typeConfigurations));
		_defaultConfiguration = defaultConfiguration ?? throw new ArgumentNullException(nameof(defaultConfiguration));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
	}

	/// <inheritdoc/>
	public string Name => "ConfiguredMapper";

	/// <inheritdoc/>
	public string SourceTransport => DefaultMessageMapper.WildcardTransport;

	/// <inheritdoc/>
	public string TargetTransport => DefaultMessageMapper.WildcardTransport;

	/// <inheritdoc/>
	public bool CanMap(string sourceTransport, string targetTransport) => true;

	/// <inheritdoc/>
	public ITransportMessageContext Map(ITransportMessageContext source, string targetTransportName)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransportName);

		// First try to delegate to a registered specific mapper
		var registeredMapper = _registry.GetMapper(source.SourceTransport ?? "unknown", targetTransportName);
		if (registeredMapper is not null && !ReferenceEquals(registeredMapper, this))
		{
			var mappedContext = registeredMapper.Map(source, targetTransportName);
			return ApplyConfiguredMappings(mappedContext, targetTransportName, source);
		}

		// Create the appropriate target context
		var target = CreateTargetContext(source.MessageId, targetTransportName);

		// Copy common properties
		target.CorrelationId = source.CorrelationId;
		target.CausationId = source.CausationId;
		target.SourceTransport = source.SourceTransport;
		target.TargetTransport = targetTransportName;
		target.Timestamp = source.Timestamp;
		target.ContentType = source.ContentType;

		// Copy headers
		target.SetHeaders(source.Headers);

		// Copy transport properties
		foreach (var property in source.GetAllTransportProperties())
		{
			target.SetTransportProperty(property.Key, property.Value);
		}

		// Apply configured mappings
		return ApplyConfiguredMappings(target, targetTransportName, source);
	}

	/// <summary>
	/// Maps a message with knowledge of its concrete type.
	/// </summary>
	/// <typeparam name="TMessage">The message type.</typeparam>
	/// <param name="source">The source context.</param>
	/// <param name="targetTransportName">The target transport name.</param>
	/// <returns>The mapped context.</returns>
	public ITransportMessageContext Map<TMessage>(ITransportMessageContext source, string targetTransportName)
		where TMessage : class
	{
		var target = Map(source, targetTransportName);

		// Apply type-specific mappings if available
		if (_typeConfigurations.TryGetValue(typeof(TMessage), out var typeConfig) && target is TransportMessageContext transportContext)
		{
			ApplyTypeConfiguration(typeConfig, transportContext, targetTransportName);
		}

		return target;
	}

	private TransportMessageContext CreateTargetContext(string messageId, string targetTransport)
	{
		if (string.Equals(targetTransport, "rabbitmq", StringComparison.OrdinalIgnoreCase))
		{
			return new RabbitMqMessageContext(messageId);
		}

		if (string.Equals(targetTransport, "kafka", StringComparison.OrdinalIgnoreCase))
		{
			return new KafkaMessageContext(messageId);
		}

		return new TransportMessageContext(messageId);
	}

	private ITransportMessageContext ApplyConfiguredMappings(
		ITransportMessageContext target,
		string targetTransportName,
		ITransportMessageContext source)
	{
		if (target is not TransportMessageContext transportContext)
		{
			return target;
		}

		// Get message type from header if available
		var messageTypeName = source.Headers.GetValueOrDefault("X-Message-Type");
		if (!string.IsNullOrEmpty(messageTypeName))
		{
			// Try to find type-specific configuration by type name
			var typeConfig = _typeConfigurations.Values.FirstOrDefault(c =>
				string.Equals(c.MessageType.Name, messageTypeName, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(c.MessageType.FullName, messageTypeName, StringComparison.OrdinalIgnoreCase));

			if (typeConfig is not null)
			{
				ApplyTypeConfiguration(typeConfig, transportContext, targetTransportName);
				return transportContext;
			}
		}

		// Apply default configuration
		ApplyDefaultConfiguration(transportContext, targetTransportName);
		return transportContext;
	}

	private void ApplyTypeConfiguration(
		MessageTypeMapperConfiguration config,
		TransportMessageContext target,
		string targetTransportName)
	{
		if (string.Equals(targetTransportName, "rabbitmq", StringComparison.OrdinalIgnoreCase))
		{
			if (config.RabbitMqConfiguration is not null && target is RabbitMqMessageContext rabbitContext)
			{
				var mappingContext = new RabbitMqMappingContext();
				config.RabbitMqConfiguration(mappingContext);
				mappingContext.ApplyTo(rabbitContext);
			}
		}
		else if (string.Equals(targetTransportName, "kafka", StringComparison.OrdinalIgnoreCase))
		{
			if (config.KafkaConfiguration is not null && target is KafkaMessageContext kafkaContext)
			{
				var mappingContext = new KafkaMappingContext();
				config.KafkaConfiguration(mappingContext);
				mappingContext.ApplyTo(kafkaContext);
			}
		}
		else if (string.Equals(targetTransportName, "azureservicebus", StringComparison.OrdinalIgnoreCase))
		{
			if (config.AzureServiceBusConfiguration is not null)
			{
				var mappingContext = new AzureServiceBusMappingContext();
				config.AzureServiceBusConfiguration(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "sqs", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(targetTransportName, "awssqs", StringComparison.OrdinalIgnoreCase))
		{
			if (config.AwsSqsConfiguration is not null)
			{
				var mappingContext = new AwsSqsMappingContext();
				config.AwsSqsConfiguration(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "sns", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(targetTransportName, "awssns", StringComparison.OrdinalIgnoreCase))
		{
			if (config.AwsSnsConfiguration is not null)
			{
				var mappingContext = new AwsSnsMappingContext();
				config.AwsSnsConfiguration(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "pubsub", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(targetTransportName, "googlepubsub", StringComparison.OrdinalIgnoreCase))
		{
			if (config.GooglePubSubConfiguration is not null)
			{
				var mappingContext = new GooglePubSubMappingContext();
				config.GooglePubSubConfiguration(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "grpc", StringComparison.OrdinalIgnoreCase))
		{
			if (config.GrpcConfiguration is not null)
			{
				var mappingContext = new GrpcMappingContext();
				config.GrpcConfiguration(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (config.CustomTransportConfigurations.TryGetValue(targetTransportName, out var customConfig))
		{
			customConfig(target);
		}
	}

	private void ApplyDefaultConfiguration(TransportMessageContext target, string targetTransportName)
	{
		if (string.Equals(targetTransportName, "rabbitmq", StringComparison.OrdinalIgnoreCase))
		{
			if (_defaultConfiguration.RabbitMqDefaults is not null && target is RabbitMqMessageContext rabbitContext)
			{
				var mappingContext = new RabbitMqMappingContext();
				_defaultConfiguration.RabbitMqDefaults(mappingContext);
				mappingContext.ApplyTo(rabbitContext);
			}
		}
		else if (string.Equals(targetTransportName, "kafka", StringComparison.OrdinalIgnoreCase))
		{
			if (_defaultConfiguration.KafkaDefaults is not null && target is KafkaMessageContext kafkaContext)
			{
				var mappingContext = new KafkaMappingContext();
				_defaultConfiguration.KafkaDefaults(mappingContext);
				mappingContext.ApplyTo(kafkaContext);
			}
		}
		else if (string.Equals(targetTransportName, "azureservicebus", StringComparison.OrdinalIgnoreCase))
		{
			if (_defaultConfiguration.AzureServiceBusDefaults is not null)
			{
				var mappingContext = new AzureServiceBusMappingContext();
				_defaultConfiguration.AzureServiceBusDefaults(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "sqs", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(targetTransportName, "awssqs", StringComparison.OrdinalIgnoreCase))
		{
			if (_defaultConfiguration.AwsSqsDefaults is not null)
			{
				var mappingContext = new AwsSqsMappingContext();
				_defaultConfiguration.AwsSqsDefaults(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
		else if (string.Equals(targetTransportName, "pubsub", StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(targetTransportName, "googlepubsub", StringComparison.OrdinalIgnoreCase))
		{
			if (_defaultConfiguration.GooglePubSubDefaults is not null)
			{
				var mappingContext = new GooglePubSubMappingContext();
				_defaultConfiguration.GooglePubSubDefaults(mappingContext);
				mappingContext.ApplyTo(target);
			}
		}
	}
}
