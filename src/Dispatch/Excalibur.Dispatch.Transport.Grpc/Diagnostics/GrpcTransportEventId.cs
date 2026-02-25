// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Event IDs for gRPC transport logging (25000-25099).
/// </summary>
public static class GrpcTransportEventId
{
	// ========================================
	// 25000-25019: Sender
	// ========================================

	/// <summary>gRPC transport sender: message sent successfully.</summary>
	public const int SenderMessageSent = 25000;

	/// <summary>gRPC transport sender: send failed.</summary>
	public const int SenderSendFailed = 25001;

	/// <summary>gRPC transport sender: batch sent.</summary>
	public const int SenderBatchSent = 25002;

	/// <summary>gRPC transport sender: batch send failed.</summary>
	public const int SenderBatchSendFailed = 25003;

	/// <summary>gRPC transport sender: disposed.</summary>
	public const int SenderDisposed = 25004;

	// ========================================
	// 25020-25039: Receiver
	// ========================================

	/// <summary>gRPC transport receiver: messages received.</summary>
	public const int ReceiverMessagesReceived = 25020;

	/// <summary>gRPC transport receiver: receive failed.</summary>
	public const int ReceiverReceiveFailed = 25021;

	/// <summary>gRPC transport receiver: message acknowledged.</summary>
	public const int ReceiverMessageAcknowledged = 25022;

	/// <summary>gRPC transport receiver: acknowledge failed.</summary>
	public const int ReceiverAcknowledgeFailed = 25023;

	/// <summary>gRPC transport receiver: message rejected.</summary>
	public const int ReceiverMessageRejected = 25024;

	/// <summary>gRPC transport receiver: reject failed.</summary>
	public const int ReceiverRejectFailed = 25025;

	/// <summary>gRPC transport receiver: disposed.</summary>
	public const int ReceiverDisposed = 25026;

	// ========================================
	// 25040-25059: Subscriber
	// ========================================

	/// <summary>gRPC transport subscriber: subscription started.</summary>
	public const int SubscriberStarted = 25040;

	/// <summary>gRPC transport subscriber: message received.</summary>
	public const int SubscriberMessageReceived = 25041;

	/// <summary>gRPC transport subscriber: message acknowledged.</summary>
	public const int SubscriberMessageAcknowledged = 25042;

	/// <summary>gRPC transport subscriber: message rejected.</summary>
	public const int SubscriberMessageRejected = 25043;

	/// <summary>gRPC transport subscriber: message requeued.</summary>
	public const int SubscriberMessageRequeued = 25044;

	/// <summary>gRPC transport subscriber: error.</summary>
	public const int SubscriberError = 25045;

	/// <summary>gRPC transport subscriber: subscription stopped.</summary>
	public const int SubscriberStopped = 25046;

	/// <summary>gRPC transport subscriber: disposed.</summary>
	public const int SubscriberDisposed = 25047;

	/// <summary>gRPC transport subscriber: stream ended by server.</summary>
	public const int SubscriberStreamEnded = 25048;
}
