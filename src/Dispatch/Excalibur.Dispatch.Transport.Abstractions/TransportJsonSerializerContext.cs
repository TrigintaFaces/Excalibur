// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// JSON serializer context for Excalibur.Dispatch.Transport.Abstractions types, enabling AOT-compatible serialization.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Source-generated JSON serialization doesn't use reflection.")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling", Justification = "Source-generated JSON serialization is AOT-compatible.")]
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata)]
// Configuration/Options types
[JsonSerializable(typeof(CloudMessagingOptions))]
[JsonSerializable(typeof(ProviderOptions))]
[JsonSerializable(typeof(BatchProcessingOptions))]
[JsonSerializable(typeof(RetryPolicy))]
// Data/Result types
[JsonSerializable(typeof(TransportMessage))]
[JsonSerializable(typeof(TransportReceivedMessage))]
[JsonSerializable(typeof(SendResult))]
[JsonSerializable(typeof(SendError))]
[JsonSerializable(typeof(BatchSendResult))]
[JsonSerializable(typeof(DeadLetterMessage))]
// Enum types
[JsonSerializable(typeof(CloudProviderType))]
[JsonSerializable(typeof(BatchCompletionStrategy))]
[JsonSerializable(typeof(BatchPriority))]
[JsonSerializable(typeof(RetryDelayStrategy))]
[JsonSerializable(typeof(MessagePriority))]
[JsonSerializable(typeof(CompressionAlgorithm))]
// Collection types
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, ProviderOptions>))]
public partial class TransportJsonSerializerContext : JsonSerializerContext
{
}
