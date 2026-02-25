// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Factory for creating and parsing message envelopes for Azure Storage Queue messages.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageEnvelopeFactory" /> class. </remarks>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="cloudEventProcessor"> The CloudEvent processor. </param>
/// <param name="logger"> The logger instance. </param>
/// <param name="serviceProvider"> The service provider. </param>
public sealed class MessageEnvelopeFactory(
	IPayloadSerializer serializer,
	ICloudEventProcessor cloudEventProcessor,
	ILogger<MessageEnvelopeFactory> logger,
	IServiceProvider serviceProvider) : IMessageEnvelopeFactory
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	private readonly IPayloadSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

	private readonly ICloudEventProcessor _cloudEventProcessor =
		cloudEventProcessor ?? throw new ArgumentNullException(nameof(cloudEventProcessor));

	private readonly ILogger<MessageEnvelopeFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

	/// <inheritdoc />
	[RequiresUnreferencedCode("Message envelope serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Message envelope serialization uses reflection to dynamically access and serialize types")]
	public string CreateEnvelope(object message, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			var envelope = new StorageQueueMessageEnvelope
			{
				MessageType = message.GetType().AssemblyQualifiedName ?? message.GetType().FullName ?? "Unknown",
				CorrelationId = context.CorrelationId,
				MessageId = context.MessageId ?? Guid.NewGuid().ToString(),
				Timestamp = DateTimeOffset.UtcNow,
				Properties = context.Items?.ToDictionary(static kv => kv.Key, static kv => kv.Value?.ToString()) ?? [],
				// Use SerializeObject with runtime type to ensure proper concrete type serialization
				Body = _serializer.SerializeObject(message, message.GetType()),
			};

			var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);

			_logger.LogDebug(
				"Created message envelope for type {MessageType} with ID {MessageId}",
				envelope.MessageType, envelope.MessageId);

			return envelopeJson;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create message envelope for message of type {MessageType}", message.GetType().Name);
			throw;
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Message envelope deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Message envelope deserialization uses reflection to dynamically create and populate types")]
	public ParsedMessageResult ParseMessage(QueueMessage queueMessage)
	{
		ArgumentNullException.ThrowIfNull(queueMessage);

		var messageText = queueMessage.Body.ToString();

		// First, try to parse as CloudEvent
		if (_cloudEventProcessor.TryParseCloudEvent(messageText, out var cloudEvent) && cloudEvent != null)
		{
			var context = CreateContext(queueMessage);
			_cloudEventProcessor.UpdateContextFromCloudEvent(context, cloudEvent);

			var dispatchEvent = _cloudEventProcessor.ConvertToDispatchEvent(cloudEvent, queueMessage, context);

			return new ParsedMessageResult
			{
				Message = dispatchEvent,
				Context = context,
				MessageType = dispatchEvent.GetType(),
				IsCloudEvent = true,
				Metadata = new Dictionary<string, object?>
(StringComparer.Ordinal)
				{
					["CloudEvent.Type"] = cloudEvent.Type,
					["CloudEvent.Source"] = cloudEvent.Source?.ToString(),
					["CloudEvent.Subject"] = cloudEvent.Subject,
				},
			};
		}

		// Try to parse as message envelope
		try
		{
			using var document = JsonDocument.Parse(messageText);
			var root = document.RootElement;

			if (root.TryGetProperty("messageType", out _) && root.TryGetProperty("body", out _))
			{
				var envelope = JsonSerializer.Deserialize<StorageQueueMessageEnvelope>(messageText, JsonOptions);
				if (envelope != null)
				{
					return ParseEnvelope(envelope, queueMessage);
				}
			}
		}
		catch (JsonException ex)
		{
			_logger.LogDebug("Failed to parse message as envelope JSON: {Error}", ex.Message);
		}

		// Fallback - treat as plain message
		return ParsePlainMessage(messageText, queueMessage);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public bool TryParseMessage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(QueueMessage queueMessage, out T? parsedMessage, out IMessageContext? context)
	{
		parsedMessage = default;
		context = null;

		try
		{
			var result = ParseMessage(queueMessage);
			context = result.Context;

			if (result.Message is T typedMessage)
			{
				parsedMessage = typedMessage;
				return true;
			}

			// Try to deserialize the message body directly if it's an envelope
			if (result.Message is StorageQueueMessageEnvelope envelope)
			{
				var deserializedMessage = _serializer.Deserialize<T>(envelope.Body);
				if (!EqualityComparer<T?>.Default.Equals(deserializedMessage, default(T?)))
				{
					parsedMessage = deserializedMessage;
					return true;
				}
			}

			return false;
		}
		catch (Exception ex)
		{
			_logger.LogDebug("Failed to parse message as type {MessageType}: {Error}", typeof(T).Name, ex.Message);
			return false;
		}
	}

	/// <inheritdoc />
	public IMessageContext CreateContext(QueueMessage queueMessage)
	{
		ArgumentNullException.ThrowIfNull(queueMessage);

		var context = MessageContext.CreateForDeserialization(_serviceProvider);
		context.MessageId = queueMessage.MessageId;
		context.CorrelationId = new CorrelationId(queueMessage.MessageId).ToString(); // Default fallback
		context.ReceivedTimestampUtc = queueMessage.InsertedOn ?? DateTimeOffset.UtcNow;

		// Add queue-specific metadata
		context.SetItem("Azure.MessageId", queueMessage.MessageId);
		context.SetItem("Azure.PopReceipt", queueMessage.PopReceipt);
		context.SetItem("Azure.DequeueCount", queueMessage.DequeueCount);
		context.SetItem("Azure.NextVisibleOn", queueMessage.NextVisibleOn);
		context.SetItem("Azure.ExpiresOn", queueMessage.ExpiresOn);
		context.SetItem("Azure.InsertedOn", queueMessage.InsertedOn);

		return context;
	}

	/// <inheritdoc />
	public bool IsValidEnvelope(QueueMessage queueMessage)
	{
		ArgumentNullException.ThrowIfNull(queueMessage);

		try
		{
			var messageText = queueMessage.Body.ToString();

			// Check if it's a CloudEvent
			if (_cloudEventProcessor.TryParseCloudEvent(messageText, out _))
			{
				return true;
			}

			// Check if it's a message envelope
			using var document = JsonDocument.Parse(messageText);
			var root = document.RootElement;

			return root.TryGetProperty("messageType", out _) &&
				   root.TryGetProperty("body", out _) &&
				   root.TryGetProperty("messageId", out _);
		}
		catch (Exception ex)
		{
			_logger.LogDebug("Message {MessageId} failed envelope validation: {Error}", queueMessage.MessageId, ex.Message);
			return false;
		}
	}

	[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2073:Target parameter argument does not satisfy 'DynamicallyAccessedMemberTypes.All' in call to target method",
		Justification = "Dynamic type resolution is required for message envelope deserialization")]
	[RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetType(String, Boolean)")]
	private static Type? ResolveMessageType(string messageTypeName)
	{
		if (string.IsNullOrWhiteSpace(messageTypeName))
		{
			return null;
		}

		try
		{
			var typeName = messageTypeName;
			var assemblySimpleName = string.Empty;

			var separatorIndex = messageTypeName.IndexOf(',', StringComparison.Ordinal);
			if (separatorIndex > 0)
			{
				typeName = messageTypeName[..separatorIndex].Trim();
				var assemblyName = messageTypeName[(separatorIndex + 1)..].Trim();
				try
				{
					assemblySimpleName = new AssemblyName(assemblyName).Name ?? string.Empty;
				}
				catch (ArgumentException)
				{
					assemblySimpleName = assemblyName;
				}
			}

			// Try searching loaded assemblies for the type name
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!string.IsNullOrEmpty(assemblySimpleName) &&
					!string.Equals(assembly.GetName().Name, assemblySimpleName, StringComparison.Ordinal))
				{
					continue;
				}

				// Note: This may cause AOT warnings but is needed for runtime type resolution
				var type = assembly.GetType(typeName, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	[RequiresDynamicCode("Calls System.Reflection.MethodInfo.MakeGenericMethod(params Type[])")]
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.AzureServiceBus.StorageQueues.Serialization.MessageEnvelopeFactory.ResolveMessageType(String)")]
	private ParsedMessageResult ParseEnvelope(StorageQueueMessageEnvelope envelope, QueueMessage queueMessage)
	{
		var context = CreateContext(queueMessage);
		context.CorrelationId = !string.IsNullOrEmpty(envelope.CorrelationId)
			? new CorrelationId(envelope.CorrelationId).ToString()
			: context.CorrelationId;
		context.MessageId = envelope.MessageId ?? context.MessageId;

		// Add envelope properties to context
		foreach (var property in envelope.Properties)
		{
			if (!string.IsNullOrEmpty(property.Value))
			{
				context.SetItem(property.Key, property.Value);
			}
		}

		// Try to resolve message type
		var messageType = ResolveMessageType(envelope.MessageType);
		object message;

		if (messageType != null)
		{
			// Use reflection to call the generic Deserialize method
			var deserializeMethod = typeof(IMessageSerializer).GetMethod(nameof(IMessageSerializer.Deserialize))!;
			var genericDeserializeMethod = deserializeMethod.MakeGenericMethod(messageType);
			message = genericDeserializeMethod.Invoke(_serializer, [envelope.Body])!;
			context.SetItem("MessageType", messageType.Name);
		}
		else
		{
			// Fallback to envelope itself
			message = envelope;
			messageType = typeof(StorageQueueMessageEnvelope);
			context.SetItem("MessageType", "StorageQueueMessageEnvelope");
		}

		return new ParsedMessageResult
		{
			Message = message,
			Context = context,
			MessageType = messageType,
			IsCloudEvent = false,
			Metadata = new Dictionary<string, object?>
(StringComparer.Ordinal)
			{
				["Envelope.MessageType"] = envelope.MessageType,
				["Envelope.Timestamp"] = envelope.Timestamp,
				["Envelope.PropertyCount"] = envelope.Properties.Count,
			},
		};
	}

	private ParsedMessageResult ParsePlainMessage(string messageText, QueueMessage queueMessage)
	{
		var context = CreateContext(queueMessage);
		context.SetItem("MessageType", "PlainText");

		return new ParsedMessageResult
		{
			Message = messageText,
			Context = context,
			MessageType = typeof(string),
			IsCloudEvent = false,
			Metadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["PlainMessage.Length"] = messageText.Length },
		};
	}
}
