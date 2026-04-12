// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Source-generated JSON serialization context for gRPC transport message types.
/// Enables AOT-compatible serialization without runtime reflection.
/// </summary>
[JsonSerializable(typeof(GrpcTransportRequest))]
[JsonSerializable(typeof(GrpcTransportResponse))]
[JsonSerializable(typeof(GrpcBatchRequest))]
[JsonSerializable(typeof(GrpcBatchResponse))]
[JsonSerializable(typeof(GrpcSubscribeRequest))]
[JsonSerializable(typeof(GrpcReceivedMessage))]
[JsonSerializable(typeof(GrpcReceiveRequest))]
[JsonSerializable(typeof(GrpcReceiveResponse))]
[JsonSerializable(typeof(GrpcAcknowledgeRequest))]
[JsonSerializable(typeof(GrpcAcknowledgeResponse))]
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GrpcJsonSerializerContext : JsonSerializerContext;
