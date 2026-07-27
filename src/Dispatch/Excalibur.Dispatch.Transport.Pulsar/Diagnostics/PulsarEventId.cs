// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar.Diagnostics;

/// <summary>
/// Event IDs for the Apache Pulsar transport (26000-26099).
/// </summary>
internal static class PulsarEventId
{
	// 26000-26049: Transport sender
	public const int TransportSenderMessageSent = 26000;
	public const int TransportSenderSendFailed = 26001;
	public const int TransportSenderBatchSent = 26002;
	public const int TransportSenderFlushed = 26003;
	public const int TransportSenderDisposed = 26004;

	// 26050-26099: Transport receiver
	public const int TransportReceiverMessageReceived = 26050;
	public const int TransportReceiverReceiveError = 26051;
	public const int TransportReceiverMessageAcknowledged = 26052;
	public const int TransportReceiverAcknowledgeError = 26053;
	public const int TransportReceiverMessageRejected = 26054;
	public const int TransportReceiverMessageRejectedRequeue = 26055;
	public const int TransportReceiverPayloadTooLarge = 26056;
	public const int TransportReceiverDisposed = 26057;
	public const int TransportReceiverUnsettledOverflow = 26058;

	// 26070-26099: Message bus
	public const int MessageBusActionSent = 26070;
	public const int MessageBusEventPublished = 26071;
	public const int MessageBusDocumentSent = 26072;
}
