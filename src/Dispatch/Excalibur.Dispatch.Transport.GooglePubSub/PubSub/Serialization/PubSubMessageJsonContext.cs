// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Source generation context for Google Cloud Pub/Sub message types. Provides AOT-compatible JSON serialization for Pub/Sub messaging.
/// </summary>
/// <remarks>
/// This context is optimized for Google Cloud Pub/Sub with:
/// - Protobuf to JSON interoperability
/// - Ordering key support
/// - Batch processing optimization
/// - Dead letter queue management.
/// </remarks>
[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true)]

// Google Pub/Sub message types (avoiding protobuf request/this.response types that cause ResourceNameType conflicts)
[JsonSerializable(typeof(PubsubMessage))]
[JsonSerializable(typeof(ReceivedMessage))]
[JsonSerializable(typeof(List<PubsubMessage>))]
[JsonSerializable(typeof(List<ReceivedMessage>))]

// Pub/Sub attributes and metadata
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(MapField<string, string>), TypeInfoPropertyName = "PubSubMapFieldStringString")]

// Batch processing types
[JsonSerializable(typeof(ProcessedMessage))]
[JsonSerializable(typeof(FailedMessage))]
[JsonSerializable(typeof(List<ProcessedMessage>))]
[JsonSerializable(typeof(List<FailedMessage>))]
[JsonSerializable(typeof(SerializedBatchResult))]

// Dead letter types (shared from Transport.Abstractions)
[JsonSerializable(typeof(DeadLetterMessage))]
[JsonSerializable(typeof(List<DeadLetterMessage>))]
[JsonSerializable(typeof(DeadLetterMetadata))]
[JsonSerializable(typeof(RetryPolicy))]

// Ordering and flow control
[JsonSerializable(typeof(OrderingKeyState))]
[JsonSerializable(typeof(FlowControlState))]
[JsonSerializable(typeof(Dictionary<string, OrderingKeyState>))]

// Schema registry types
[JsonSerializable(typeof(SchemaDefinition))]
[JsonSerializable(typeof(Dictionary<string, SchemaDefinition>))]

// Common types
[JsonSerializable(typeof(ByteString))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(ReadOnlyMemory<byte>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<string>))]
public partial class PubSubMessageJsonContext : JsonSerializerContext;
