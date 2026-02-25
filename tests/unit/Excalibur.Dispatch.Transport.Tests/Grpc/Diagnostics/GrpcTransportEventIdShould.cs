// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcTransportEventIdShould
{
	[Fact]
	public void HaveSenderEventIdsInRange25000To25019()
	{
		// Assert
		GrpcTransportEventId.SenderMessageSent.ShouldBe(25000);
		GrpcTransportEventId.SenderSendFailed.ShouldBe(25001);
		GrpcTransportEventId.SenderBatchSent.ShouldBe(25002);
		GrpcTransportEventId.SenderBatchSendFailed.ShouldBe(25003);
		GrpcTransportEventId.SenderDisposed.ShouldBe(25004);
	}

	[Fact]
	public void HaveReceiverEventIdsInRange25020To25039()
	{
		// Assert
		GrpcTransportEventId.ReceiverMessagesReceived.ShouldBe(25020);
		GrpcTransportEventId.ReceiverReceiveFailed.ShouldBe(25021);
		GrpcTransportEventId.ReceiverMessageAcknowledged.ShouldBe(25022);
		GrpcTransportEventId.ReceiverAcknowledgeFailed.ShouldBe(25023);
		GrpcTransportEventId.ReceiverMessageRejected.ShouldBe(25024);
		GrpcTransportEventId.ReceiverRejectFailed.ShouldBe(25025);
		GrpcTransportEventId.ReceiverDisposed.ShouldBe(25026);
	}

	[Fact]
	public void HaveSubscriberEventIdsInRange25040To25059()
	{
		// Assert
		GrpcTransportEventId.SubscriberStarted.ShouldBe(25040);
		GrpcTransportEventId.SubscriberMessageReceived.ShouldBe(25041);
		GrpcTransportEventId.SubscriberMessageAcknowledged.ShouldBe(25042);
		GrpcTransportEventId.SubscriberMessageRejected.ShouldBe(25043);
		GrpcTransportEventId.SubscriberMessageRequeued.ShouldBe(25044);
		GrpcTransportEventId.SubscriberError.ShouldBe(25045);
		GrpcTransportEventId.SubscriberStopped.ShouldBe(25046);
		GrpcTransportEventId.SubscriberDisposed.ShouldBe(25047);
		GrpcTransportEventId.SubscriberStreamEnded.ShouldBe(25048);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var allIds = new[]
		{
			GrpcTransportEventId.SenderMessageSent,
			GrpcTransportEventId.SenderSendFailed,
			GrpcTransportEventId.SenderBatchSent,
			GrpcTransportEventId.SenderBatchSendFailed,
			GrpcTransportEventId.SenderDisposed,
			GrpcTransportEventId.ReceiverMessagesReceived,
			GrpcTransportEventId.ReceiverReceiveFailed,
			GrpcTransportEventId.ReceiverMessageAcknowledged,
			GrpcTransportEventId.ReceiverAcknowledgeFailed,
			GrpcTransportEventId.ReceiverMessageRejected,
			GrpcTransportEventId.ReceiverRejectFailed,
			GrpcTransportEventId.ReceiverDisposed,
			GrpcTransportEventId.SubscriberStarted,
			GrpcTransportEventId.SubscriberMessageReceived,
			GrpcTransportEventId.SubscriberMessageAcknowledged,
			GrpcTransportEventId.SubscriberMessageRejected,
			GrpcTransportEventId.SubscriberMessageRequeued,
			GrpcTransportEventId.SubscriberError,
			GrpcTransportEventId.SubscriberStopped,
			GrpcTransportEventId.SubscriberDisposed,
			GrpcTransportEventId.SubscriberStreamEnded,
		};

		// Assert
		allIds.Distinct().Count().ShouldBe(allIds.Length);
	}
}
