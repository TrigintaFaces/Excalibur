// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// gRPC request message for sending a single transport message.
/// </summary>
internal sealed class GrpcTransportRequest
{
	/// <summary>Gets or sets the message ID.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the message body as Base64.</summary>
	public string Body { get; set; } = string.Empty;

	/// <summary>Gets or sets the content type.</summary>
	public string? ContentType { get; set; }

	/// <summary>Gets or sets the message type.</summary>
	public string? MessageType { get; set; }

	/// <summary>Gets or sets the correlation ID.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Gets or sets the subject.</summary>
	public string? Subject { get; set; }

	/// <summary>Gets or sets the destination.</summary>
	public string? Destination { get; set; }

	/// <summary>Gets or sets the message properties.</summary>
	public Dictionary<string, string> Properties { get; set; } = [];
}

/// <summary>
/// gRPC response message for a send operation.
/// </summary>
internal sealed class GrpcTransportResponse
{
	/// <summary>Gets or sets whether the send succeeded.</summary>
	public bool IsSuccess { get; set; }

	/// <summary>Gets or sets the broker-assigned message ID.</summary>
	public string? MessageId { get; set; }

	/// <summary>Gets or sets the error code if failed.</summary>
	public string? ErrorCode { get; set; }

	/// <summary>Gets or sets the error message if failed.</summary>
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// gRPC request message for batch send.
/// </summary>
internal sealed class GrpcBatchRequest
{
	/// <summary>Gets or sets the batch of messages.</summary>
	public List<GrpcTransportRequest> Messages { get; set; } = [];
}

/// <summary>
/// gRPC response message for batch send.
/// </summary>
internal sealed class GrpcBatchResponse
{
	/// <summary>Gets or sets the individual results.</summary>
	public List<GrpcTransportResponse> Results { get; set; } = [];
}

/// <summary>
/// gRPC request message for subscribing to a stream.
/// </summary>
internal sealed class GrpcSubscribeRequest
{
	/// <summary>Gets or sets the source to subscribe to.</summary>
	public string Source { get; set; } = string.Empty;
}

/// <summary>
/// gRPC message representing a received message from the server stream.
/// </summary>
internal sealed class GrpcReceivedMessage
{
	/// <summary>Gets or sets the message ID.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the message body as Base64.</summary>
	public string Body { get; set; } = string.Empty;

	/// <summary>Gets or sets the content type.</summary>
	public string? ContentType { get; set; }

	/// <summary>Gets or sets the message type.</summary>
	public string? MessageType { get; set; }

	/// <summary>Gets or sets the correlation ID.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Gets or sets the subject.</summary>
	public string? Subject { get; set; }

	/// <summary>Gets or sets the delivery count.</summary>
	public int DeliveryCount { get; set; }

	/// <summary>Gets or sets the source.</summary>
	public string? Source { get; set; }

	/// <summary>Gets or sets the message properties.</summary>
	public Dictionary<string, string> Properties { get; set; } = [];

	/// <summary>Gets or sets the provider-specific data.</summary>
	public Dictionary<string, string> ProviderData { get; set; } = [];
}

/// <summary>
/// gRPC request for pulling messages.
/// </summary>
internal sealed class GrpcReceiveRequest
{
	/// <summary>Gets or sets the source to receive from.</summary>
	public string Source { get; set; } = string.Empty;

	/// <summary>Gets or sets the maximum number of messages to receive.</summary>
	public int MaxMessages { get; set; }
}

/// <summary>
/// gRPC response containing received messages.
/// </summary>
internal sealed class GrpcReceiveResponse
{
	/// <summary>Gets or sets the received messages.</summary>
	public List<GrpcReceivedMessage> Messages { get; set; } = [];
}

/// <summary>
/// gRPC request for acknowledging a message.
/// </summary>
internal sealed class GrpcAcknowledgeRequest
{
	/// <summary>Gets or sets the message ID to acknowledge.</summary>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>Gets or sets the action (acknowledge, reject, requeue).</summary>
	public string Action { get; set; } = string.Empty;

	/// <summary>Gets or sets the rejection reason.</summary>
	public string? Reason { get; set; }
}

/// <summary>
/// gRPC response for acknowledge operation.
/// </summary>
internal sealed class GrpcAcknowledgeResponse
{
	/// <summary>Gets or sets whether the operation succeeded.</summary>
	public bool IsSuccess { get; set; }
}

/// <summary>
/// Interprets the server's settlement response for the acknowledge, reject and requeue actions, which all
/// share the one Acknowledge RPC and so must read its result the same way.
/// </summary>
internal static class GrpcSettlement
{
	/// <summary>
	/// Determines whether the server accepted the settlement it was asked to perform.
	/// </summary>
	/// <param name="response">The settlement response, or <see langword="null"/> when the server sent no body.</param>
	/// <returns><see langword="true"/> only when the server explicitly reported success.</returns>
	/// <remarks>
	/// Fail-closed: a null body and an unset <see cref="GrpcAcknowledgeResponse.IsSuccess"/> both read as
	/// NOT accepted. The wire contract declares the field, so a server that omits it has not reported the
	/// settlement done — and of the two ways to be wrong, reporting an accepted settlement that never
	/// happened loses the message, while reporting an unaccepted one that did leaves it to be redelivered.
	/// </remarks>
	public static bool IsAccepted(GrpcAcknowledgeResponse? response) => response is { IsSuccess: true };
}
