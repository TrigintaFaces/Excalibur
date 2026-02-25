// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of <see cref="IMessageMappingBuilder"/> for fluent message mapping configuration.
/// </summary>
public sealed class MessageMappingBuilder : IMessageMappingBuilder
{
	private readonly IServiceCollection _services;
	private readonly MessageMapperRegistry _registry;
	private readonly ConcurrentDictionary<Type, MessageTypeMapperConfiguration> _typeConfigurations = new();
	private readonly DefaultMappingConfiguration _defaultConfiguration = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageMappingBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="registry">The mapper registry.</param>
	public MessageMappingBuilder(IServiceCollection services, MessageMapperRegistry registry)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
	}

	/// <summary>
	/// Gets the default mapping configuration.
	/// </summary>
	internal DefaultMappingConfiguration DefaultConfiguration => _defaultConfiguration;

	/// <summary>
	/// Gets the type-specific configurations.
	/// </summary>
	internal ConcurrentDictionary<Type, MessageTypeMapperConfiguration> TypeConfigurations => _typeConfigurations;

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> MapMessage<TMessage>()
		where TMessage : class
	{
		var config = _typeConfigurations.GetOrAdd(typeof(TMessage), _ => new MessageTypeMapperConfiguration(typeof(TMessage)));
		return new MessageTypeMappingBuilder<TMessage>(this, config);
	}

	/// <inheritdoc/>
	public IMessageMappingBuilder RegisterMapper(IMessageMapper mapper)
	{
		ArgumentNullException.ThrowIfNull(mapper);
		_registry.Register(mapper);
		return this;
	}

	/// <inheritdoc/>
	public IMessageMappingBuilder RegisterMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMapper>()
		where TMapper : class, IMessageMapper
	{
		_services.TryAddSingleton<TMapper>();
		_ = _services.AddSingleton<IMessageMapper, TMapper>();
		return this;
	}

	/// <inheritdoc/>
	public IMessageMappingBuilder UseDefaultMappers()
	{
		_ = _registry.RegisterDefaultMappers();
		return this;
	}

	/// <inheritdoc/>
	public IMessageMappingBuilder ConfigureDefaults(Action<IDefaultMappingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var defaultBuilder = new DefaultMappingBuilder(_defaultConfiguration);
		configure(defaultBuilder);
		return this;
	}

	/// <summary>
	/// Builds the message type mapper that applies configured mappings.
	/// </summary>
	/// <returns>A message mapper that applies type-specific configurations.</returns>
	public IMessageMapper Build()
	{
		return new ConfiguredMessageMapper(_typeConfigurations, _defaultConfiguration, _registry);
	}
}

/// <summary>
/// Builder for configuring transport-specific mappings for a message type.
/// </summary>
/// <typeparam name="TMessage">The message type being configured.</typeparam>
public sealed class MessageTypeMappingBuilder<TMessage> : IMessageTypeMappingBuilder<TMessage>
	where TMessage : class
{
	private readonly MessageMappingBuilder _parent;
	private readonly MessageTypeMapperConfiguration _config;

	internal MessageTypeMappingBuilder(MessageMappingBuilder parent, MessageTypeMapperConfiguration config)
	{
		_parent = parent ?? throw new ArgumentNullException(nameof(parent));
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToRabbitMq(Action<IRabbitMqMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.RabbitMqConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToKafka(Action<IKafkaMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.KafkaConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToAzureServiceBus(Action<IAzureServiceBusMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.AzureServiceBusConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToAwsSqs(Action<IAwsSqsMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.AwsSqsConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToAwsSns(Action<IAwsSnsMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.AwsSnsConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToGooglePubSub(Action<IGooglePubSubMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.GooglePubSubConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToGrpc(Action<IGrpcMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.GrpcConfiguration = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageTypeMappingBuilder<TMessage> ToTransport(string transportName, Action<ITransportMessageContext> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(configure);
		_config.CustomTransportConfigurations[transportName] = configure;
		return this;
	}

	/// <inheritdoc/>
	public IMessageMappingBuilder And() => _parent;
}

/// <summary>
/// Builder for configuring default mapping behavior.
/// </summary>
public sealed class DefaultMappingBuilder : IDefaultMappingBuilder
{
	private readonly DefaultMappingConfiguration _config;

	internal DefaultMappingBuilder(DefaultMappingConfiguration config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc/>
	public IDefaultMappingBuilder ForRabbitMq(Action<IRabbitMqMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.RabbitMqDefaults = configure;
		return this;
	}

	/// <inheritdoc/>
	public IDefaultMappingBuilder ForKafka(Action<IKafkaMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.KafkaDefaults = configure;
		return this;
	}

	/// <inheritdoc/>
	public IDefaultMappingBuilder ForAzureServiceBus(Action<IAzureServiceBusMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.AzureServiceBusDefaults = configure;
		return this;
	}

	/// <inheritdoc/>
	public IDefaultMappingBuilder ForAwsSqs(Action<IAwsSqsMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.AwsSqsDefaults = configure;
		return this;
	}

	/// <inheritdoc/>
	public IDefaultMappingBuilder ForGooglePubSub(Action<IGooglePubSubMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_config.GooglePubSubDefaults = configure;
		return this;
	}
}

/// <summary>
/// Configuration for message type-specific mappings.
/// </summary>
internal sealed class MessageTypeMapperConfiguration
{
	public MessageTypeMapperConfiguration(Type messageType)
	{
		MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
	}

	public Type MessageType { get; }
	public Action<IRabbitMqMappingContext>? RabbitMqConfiguration { get; set; }
	public Action<IKafkaMappingContext>? KafkaConfiguration { get; set; }
	public Action<IAzureServiceBusMappingContext>? AzureServiceBusConfiguration { get; set; }
	public Action<IAwsSqsMappingContext>? AwsSqsConfiguration { get; set; }
	public Action<IAwsSnsMappingContext>? AwsSnsConfiguration { get; set; }
	public Action<IGooglePubSubMappingContext>? GooglePubSubConfiguration { get; set; }
	public Action<IGrpcMappingContext>? GrpcConfiguration { get; set; }

	public Dictionary<string, Action<ITransportMessageContext>> CustomTransportConfigurations { get; } =
		new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration for default mappings applied when no type-specific mapping exists.
/// </summary>
internal sealed class DefaultMappingConfiguration
{
	public Action<IRabbitMqMappingContext>? RabbitMqDefaults { get; set; }
	public Action<IKafkaMappingContext>? KafkaDefaults { get; set; }
	public Action<IAzureServiceBusMappingContext>? AzureServiceBusDefaults { get; set; }
	public Action<IAwsSqsMappingContext>? AwsSqsDefaults { get; set; }
	public Action<IGooglePubSubMappingContext>? GooglePubSubDefaults { get; set; }
}
