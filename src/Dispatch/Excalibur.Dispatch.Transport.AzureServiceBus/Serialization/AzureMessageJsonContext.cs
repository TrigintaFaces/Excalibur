// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

using Azure.Messaging.EventHubs;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues.Models;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Source generation context for Azure messaging types. Provides AOT-compatible JSON serialization for Service Bus, Event Hubs, and
/// Storage Queues.
/// </summary>
/// <remarks>
/// This context is optimized for Azure messaging with:
/// - Service Bus message and session support
/// - Event Hubs batch processing
/// - Storage Queue message handling
/// - Dead letter queue management.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]

// Azure Service this.Bus types
[JsonSerializable(typeof(ServiceBusMessage))]
[JsonSerializable(typeof(ServiceBusReceivedMessage))]
[JsonSerializable(typeof(List<ServiceBusMessage>))]
[JsonSerializable(typeof(List<ServiceBusReceivedMessage>))]
[JsonSerializable(typeof(ServiceBusMessageBatch))]

// Session this.state is handled through custom SessionState type Azure Event Hubs types
[JsonSerializable(typeof(EventData))]

// EventDataBatch is not serializable - only EventData items
[JsonSerializable(typeof(List<EventData>))]
[JsonSerializable(typeof(EventHubProperties))]
[JsonSerializable(typeof(PartitionProperties))]

// Azure Storage Queue types
[JsonSerializable(typeof(QueueMessage))]
[JsonSerializable(typeof(SendReceipt))]
[JsonSerializable(typeof(List<QueueMessage>))]
[JsonSerializable(typeof(QueueProperties))]

// Message properties and this.metadata
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(BinaryData))]

// Session and partitioning
[JsonSerializable(typeof(ServiceBusSessionState))]
[JsonSerializable(typeof(PartitionContext))]
[JsonSerializable(typeof(CheckpointData))]

// Dead letter and retry
[JsonSerializable(typeof(DeadLetterInfo))]
[JsonSerializable(typeof(RetryInfo))]
[JsonSerializable(typeof(List<DeadLetterInfo>))]

// Common Azure types
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(ReadOnlyMemory<byte>))]
[JsonSerializable(typeof(ArraySegment<byte>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
public partial class AzureMessageJsonContext : JsonSerializerContext
{
	/// <summary>
	/// Gets the singleton instance for Azure message serialization.
	/// </summary>
	/// <value>
	/// The singleton instance for Azure message serialization.
	/// </value>
	public static AzureMessageJsonContext Instance { get; } = new(GetDefaultOptions());

	/// <summary>
	/// Creates default JSON serializer options for Azure messages.
	/// </summary>
	private static JsonSerializerOptions GetDefaultOptions()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			ReferenceHandler = ReferenceHandler.IgnoreCycles,
			NumberHandling = JsonNumberHandling.AllowReadingFromString,
			MaxDepth = 32,
		};

		options.Converters.Add(new BinaryDataConverter());
		options.Converters.Add(new TimeSpanConverter());

		return options;
	}
}
