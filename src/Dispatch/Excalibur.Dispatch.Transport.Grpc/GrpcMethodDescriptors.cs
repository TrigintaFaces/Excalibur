// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Defines gRPC method descriptors for the dispatch transport service.
/// Uses manual method definitions instead of Grpc.Tools code generation.
/// </summary>
internal static class GrpcMethodDescriptors
{
	/// <summary>
	/// Creates a unary method descriptor for sending a single message.
	/// </summary>
	/// <param name="methodPath">The fully qualified gRPC method path.</param>
	/// <returns>The method descriptor.</returns>
	public static Method<GrpcTransportRequest, GrpcTransportResponse> CreateSendMethod(string methodPath) =>
		new(
			MethodType.Unary,
			ExtractServiceName(methodPath),
			ExtractMethodName(methodPath),
			GrpcTransportMarshaller.RequestMarshaller,
			GrpcTransportMarshaller.ResponseMarshaller);

	/// <summary>
	/// Creates a unary method descriptor for batch send.
	/// </summary>
	/// <param name="methodPath">The fully qualified gRPC method path.</param>
	/// <returns>The method descriptor.</returns>
	public static Method<GrpcBatchRequest, GrpcBatchResponse> CreateSendBatchMethod(string methodPath) =>
		new(
			MethodType.Unary,
			ExtractServiceName(methodPath),
			ExtractMethodName(methodPath),
			GrpcTransportMarshaller.BatchRequestMarshaller,
			GrpcTransportMarshaller.BatchResponseMarshaller);

	/// <summary>
	/// Creates a unary method descriptor for receiving messages.
	/// </summary>
	/// <param name="methodPath">The fully qualified gRPC method path.</param>
	/// <returns>The method descriptor.</returns>
	public static Method<GrpcReceiveRequest, GrpcReceiveResponse> CreateReceiveMethod(string methodPath) =>
		new(
			MethodType.Unary,
			ExtractServiceName(methodPath),
			ExtractMethodName(methodPath),
			GrpcTransportMarshaller.ReceiveRequestMarshaller,
			GrpcTransportMarshaller.ReceiveResponseMarshaller);

	/// <summary>
	/// Creates a unary method descriptor for acknowledging messages.
	/// </summary>
	/// <param name="methodPath">The fully qualified gRPC method path.</param>
	/// <returns>The method descriptor.</returns>
	public static Method<GrpcAcknowledgeRequest, GrpcAcknowledgeResponse> CreateAcknowledgeMethod(string methodPath) =>
		new(
			MethodType.Unary,
			ExtractServiceName(methodPath),
			ExtractMethodName(methodPath),
			GrpcTransportMarshaller.AcknowledgeRequestMarshaller,
			GrpcTransportMarshaller.AcknowledgeResponseMarshaller);

	/// <summary>
	/// Creates a server streaming method descriptor for subscribing to messages.
	/// </summary>
	/// <param name="methodPath">The fully qualified gRPC method path.</param>
	/// <returns>The method descriptor.</returns>
	public static Method<GrpcSubscribeRequest, GrpcReceivedMessage> CreateSubscribeMethod(string methodPath) =>
		new(
			MethodType.ServerStreaming,
			ExtractServiceName(methodPath),
			ExtractMethodName(methodPath),
			GrpcTransportMarshaller.SubscribeRequestMarshaller,
			GrpcTransportMarshaller.ReceivedMessageMarshaller);

	private static string ExtractServiceName(string methodPath)
	{
		// "/dispatch.transport.DispatchTransport/Send" -> "dispatch.transport.DispatchTransport"
		var trimmed = methodPath.TrimStart('/');
		var lastSlash = trimmed.LastIndexOf('/');
		return lastSlash >= 0 ? trimmed[..lastSlash] : trimmed;
	}

	private static string ExtractMethodName(string methodPath)
	{
		// "/dispatch.transport.DispatchTransport/Send" -> "Send"
		var lastSlash = methodPath.LastIndexOf('/');
		return lastSlash >= 0 ? methodPath[(lastSlash + 1)..] : methodPath;
	}
}
