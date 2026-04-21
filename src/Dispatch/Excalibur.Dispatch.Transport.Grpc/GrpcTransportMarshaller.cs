// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Provides gRPC marshallers for transport message types using source-generated JSON serialization.
/// </summary>
/// <remarks>
/// Uses <see cref="GrpcJsonSerializerContext"/> for AOT-compatible serialization.
/// All message types are concrete DTOs with compile-time-known schemas.
/// </remarks>
internal static class GrpcTransportMarshaller
{
	/// <summary>
	/// Gets the marshaller for <see cref="GrpcTransportRequest"/>.
	/// </summary>
	public static Marshaller<GrpcTransportRequest> RequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, GrpcJsonSerializerContext.Default.GrpcTransportRequest),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcTransportRequest)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcTransportResponse"/>.
	/// </summary>
	public static Marshaller<GrpcTransportResponse> ResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, GrpcJsonSerializerContext.Default.GrpcTransportResponse),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcTransportResponse)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcBatchRequest"/>.
	/// </summary>
	public static Marshaller<GrpcBatchRequest> BatchRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, GrpcJsonSerializerContext.Default.GrpcBatchRequest),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcBatchRequest)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcBatchResponse"/>.
	/// </summary>
	public static Marshaller<GrpcBatchResponse> BatchResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, GrpcJsonSerializerContext.Default.GrpcBatchResponse),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcBatchResponse)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcSubscribeRequest"/>.
	/// </summary>
	public static Marshaller<GrpcSubscribeRequest> SubscribeRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, GrpcJsonSerializerContext.Default.GrpcSubscribeRequest),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcSubscribeRequest)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceivedMessage"/>.
	/// </summary>
	public static Marshaller<GrpcReceivedMessage> ReceivedMessageMarshaller { get; } =
		Marshallers.Create(
			static msg => JsonSerializer.SerializeToUtf8Bytes(msg, GrpcJsonSerializerContext.Default.GrpcReceivedMessage),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcReceivedMessage)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceiveRequest"/>.
	/// </summary>
	public static Marshaller<GrpcReceiveRequest> ReceiveRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, GrpcJsonSerializerContext.Default.GrpcReceiveRequest),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcReceiveRequest)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceiveResponse"/>.
	/// </summary>
	public static Marshaller<GrpcReceiveResponse> ReceiveResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, GrpcJsonSerializerContext.Default.GrpcReceiveResponse),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcReceiveResponse)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcAcknowledgeRequest"/>.
	/// </summary>
	public static Marshaller<GrpcAcknowledgeRequest> AcknowledgeRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, GrpcJsonSerializerContext.Default.GrpcAcknowledgeRequest),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcAcknowledgeRequest)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcAcknowledgeResponse"/>.
	/// </summary>
	public static Marshaller<GrpcAcknowledgeResponse> AcknowledgeResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, GrpcJsonSerializerContext.Default.GrpcAcknowledgeResponse),
			static bytes => JsonSerializer.Deserialize(bytes, GrpcJsonSerializerContext.Default.GrpcAcknowledgeResponse)!);
}
