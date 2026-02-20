// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Provides gRPC marshallers for transport message types using JSON serialization.
/// </summary>
/// <remarks>
/// Uses manual gRPC channel/call patterns instead of Grpc.Tools code generation
/// to avoid build-time proto compilation complexity. The proto file is provided
/// as a reference for server implementations.
/// </remarks>
internal static class GrpcTransportMarshaller
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
	};

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcTransportRequest"/>.
	/// </summary>
	public static Marshaller<GrpcTransportRequest> RequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcTransportRequest>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcTransportResponse"/>.
	/// </summary>
	public static Marshaller<GrpcTransportResponse> ResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcTransportResponse>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcBatchRequest"/>.
	/// </summary>
	public static Marshaller<GrpcBatchRequest> BatchRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcBatchRequest>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcBatchResponse"/>.
	/// </summary>
	public static Marshaller<GrpcBatchResponse> BatchResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcBatchResponse>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcSubscribeRequest"/>.
	/// </summary>
	public static Marshaller<GrpcSubscribeRequest> SubscribeRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcSubscribeRequest>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceivedMessage"/>.
	/// </summary>
	public static Marshaller<GrpcReceivedMessage> ReceivedMessageMarshaller { get; } =
		Marshallers.Create(
			static msg => JsonSerializer.SerializeToUtf8Bytes(msg, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcReceivedMessage>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceiveRequest"/>.
	/// </summary>
	public static Marshaller<GrpcReceiveRequest> ReceiveRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcReceiveRequest>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcReceiveResponse"/>.
	/// </summary>
	public static Marshaller<GrpcReceiveResponse> ReceiveResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcReceiveResponse>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcAcknowledgeRequest"/>.
	/// </summary>
	public static Marshaller<GrpcAcknowledgeRequest> AcknowledgeRequestMarshaller { get; } =
		Marshallers.Create(
			static request => JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcAcknowledgeRequest>(bytes, SerializerOptions)!);

	/// <summary>
	/// Gets the marshaller for <see cref="GrpcAcknowledgeResponse"/>.
	/// </summary>
	public static Marshaller<GrpcAcknowledgeResponse> AcknowledgeResponseMarshaller { get; } =
		Marshallers.Create(
			static response => JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions),
			static bytes => JsonSerializer.Deserialize<GrpcAcknowledgeResponse>(bytes, SerializerOptions)!);
}
