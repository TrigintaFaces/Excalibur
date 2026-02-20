// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Outbox;

/// <summary>
/// Default implementation of <see cref="IOutboxMessageMapper"/> that integrates
/// the outbox pattern with the message mapping system.
/// </summary>
/// <remarks>
/// <para>
/// This mapper bridges outbound messages from the outbox store with the transport
/// message mapping infrastructure. It creates transport contexts from outbound
/// messages and applies configured mappings for target transports.
/// </para>
/// </remarks>
public sealed class OutboxMessageMapper : IOutboxMessageMapper
{
	private readonly IMessageMapper? _messageMapper;
	private readonly IMessageMapperRegistry? _mapperRegistry;
	private readonly IMessageRoutingConfiguration? _routingConfiguration;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxMessageMapper"/> class.
	/// </summary>
	/// <param name="messageMapper">Optional message mapper for transport transformations.</param>
	/// <param name="mapperRegistry">Optional mapper registry for specific transport combinations.</param>
	/// <param name="routingConfiguration">Optional routing configuration for multi-transport routing.</param>
	public OutboxMessageMapper(
		IMessageMapper? messageMapper = null,
		IMessageMapperRegistry? mapperRegistry = null,
		IMessageRoutingConfiguration? routingConfiguration = null)
	{
		_messageMapper = messageMapper;
		_mapperRegistry = mapperRegistry;
		_routingConfiguration = routingConfiguration;
	}

	/// <inheritdoc/>
	public ITransportMessageContext CreateContext(OutboundMessage message, string targetTransport)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransport);

		var context = CreateTransportSpecificContext(message.Id, targetTransport);

		// Copy properties from outbound message
		context.CorrelationId = message.CorrelationId;
		context.CausationId = message.CausationId;
		context.TargetTransport = targetTransport;

		// Set message type header
		if (!string.IsNullOrEmpty(message.MessageType))
		{
			context.SetHeader("X-Message-Type", message.MessageType);
		}

		// Copy headers from outbound message
		foreach (var header in message.Headers)
		{
			if (header.Value is string stringValue)
			{
				context.SetHeader(header.Key, stringValue);
			}
			else
			{
				context.SetTransportProperty(header.Key, header.Value);
			}
		}

		// Set destination as transport property
		if (!string.IsNullOrEmpty(message.Destination))
		{
			context.SetTransportProperty("Destination", message.Destination);
		}

		// Set priority if specified
		if (message.Priority > 0)
		{
			context.SetTransportProperty("Priority", message.Priority);
		}

		return context;
	}

	/// <inheritdoc/>
	public ITransportMessageContext MapToTransport(
		OutboundMessage message,
		ITransportMessageContext sourceContext,
		string targetTransport)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(sourceContext);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransport);

		// First, try to find a specific mapper in the registry (most specific takes priority)
		if (_mapperRegistry is not null)
		{
			var sourceTransport = sourceContext.SourceTransport ?? "outbox";
			var specificMapper = _mapperRegistry.GetMapper(sourceTransport, targetTransport);

			if (specificMapper is not null)
			{
				return specificMapper.Map(sourceContext, targetTransport);
			}
		}

		// Fall back to the general message mapper (wildcards, defaults)
		if (_messageMapper is not null)
		{
			return _messageMapper.Map(sourceContext, targetTransport);
		}

		// No mapper available - return the source context with updated target
		if (sourceContext is TransportMessageContext transportContext)
		{
			transportContext.TargetTransport = targetTransport;
		}

		return sourceContext;
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<string> GetTargetTransports(string messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

		if (_routingConfiguration is not null)
		{
			return _routingConfiguration.GetTargetTransports(messageType);
		}

		return Array.Empty<string>();
	}

	private TransportMessageContext CreateTransportSpecificContext(string messageId, string targetTransport)
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
}

/// <summary>
/// Configuration interface for message routing to multiple transports.
/// </summary>
public interface IMessageRoutingConfiguration
{
	/// <summary>
	/// Gets the default transports to use when no specific routing is configured.
	/// </summary>
	IReadOnlyCollection<string> DefaultTransports { get; }

	/// <summary>
	/// Gets the target transports configured for a message type.
	/// </summary>
	/// <param name="messageType">The fully qualified message type name.</param>
	/// <returns>A collection of target transport names.</returns>
	IReadOnlyCollection<string> GetTargetTransports(string messageType);
}
