// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.IO;
using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportFailureInjectionShould
{
	private static TransportMessage CreateMessage(string id = "msg-1") =>
		new()
		{
			Id = id,
			Body = Encoding.UTF8.GetBytes($"payload-{id}"),
			ContentType = "application/json",
			MessageType = "TestEvent",
		};

	private static TransportReceivedMessage CreateReceivedMessage(string id = "rcv-1") =>
		new()
		{
			Id = id,
			Body = Encoding.UTF8.GetBytes($"payload-{id}"),
			ContentType = "application/json",
			MessageType = "TestEvent",
		};

	#region Send Failure Tests

	[Fact]
	public async Task PropagateIOExceptionFromSendAsync()
	{
		// Arrange
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ThrowsAsync(new IOException("Connection reset by peer"));

		// Act & Assert
		await Should.ThrowAsync<IOException>(
			() => sender.SendAsync(CreateMessage(), CancellationToken.None));
	}

	[Fact]
	public async Task PropagateTimeoutExceptionFromSendAsync()
	{
		// Arrange
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ThrowsAsync(new TimeoutException("Send operation timed out"));

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			() => sender.SendAsync(CreateMessage(), CancellationToken.None));
	}

	[Fact]
	public async Task HandleSendFailureMidBatch()
	{
		// Arrange -- sender succeeds for first 3, fails on 4th
		var callCount = 0;
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				if (callCount >= 4)
				{
					throw new IOException("Connection dropped mid-batch");
				}

				return Task.FromResult(new SendResult { IsSuccess = true, MessageId = $"ack-{callCount}" });
			});

		var messages = Enumerable.Range(1, 5).Select(i => CreateMessage($"msg-{i}")).ToList();

		// Act -- send messages one by one, tracking successes before failure
		var successCount = 0;
		Exception? caughtException = null;

		foreach (var msg in messages)
		{
			try
			{
				var result = await sender.SendAsync(msg, CancellationToken.None);
				if (result.IsSuccess) successCount++;
			}
			catch (IOException ex)
			{
				caughtException = ex;
				break;
			}
		}

		// Assert -- first 3 succeeded, then failure
		successCount.ShouldBe(3);
		caughtException.ShouldNotBeNull();
		caughtException.ShouldBeOfType<IOException>();
	}

	[Fact]
	public async Task HandlePartialBatchSendResult()
	{
		// Arrange -- batch send returns partial success
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var msgs = call.GetArgument<IReadOnlyList<TransportMessage>>(0)!;
				var results = msgs.Select((m, i) => new SendResult
				{
					IsSuccess = i < 3, // First 3 succeed, rest fail
					MessageId = i < 3 ? $"ack-{i}" : null,
				}).ToList();

				return Task.FromResult(new BatchSendResult
				{
					TotalMessages = msgs.Count,
					SuccessCount = 3,
					FailureCount = msgs.Count - 3,
					Results = results,
				});
			});

		var batch = Enumerable.Range(1, 5).Select(i => CreateMessage($"msg-{i}")).ToList();

		// Act
		var result = await sender.SendBatchAsync(batch, CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(5);
		result.SuccessCount.ShouldBe(3);
		result.FailureCount.ShouldBe(2);
		result.IsCompleteSuccess.ShouldBeFalse();
		result.Results.Count(r => r.IsSuccess).ShouldBe(3);
	}

	#endregion

	#region Receive Failure Tests

	[Fact]
	public async Task PropagateIOExceptionFromReceiveAsync()
	{
		// Arrange
		var receiver = A.Fake<ITransportReceiver>();
		A.CallTo(() => receiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.ThrowsAsync(new IOException("Connection refused"));

		// Act & Assert
		await Should.ThrowAsync<IOException>(
			() => receiver.ReceiveAsync(10, CancellationToken.None));
	}

	[Fact]
	public async Task HandleReceiveReturningEmptyOnTransientError()
	{
		// Arrange -- receiver returns empty list (transport recovered)
		var receiver = A.Fake<ITransportReceiver>();
		A.CallTo(() => receiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([]));

		// Act
		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldBeEmpty();
	}

	#endregion

	#region Connection Drop / Cancellation Tests

	[Fact]
	public async Task ThrowOperationCanceledWhenConnectionDropsDuringSend()
	{
		// Arrange -- simulate connection drop via cancellation
		using var cts = new CancellationTokenSource();
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				cts.Cancel(); // Simulate connection drop
				ct.ThrowIfCancellationRequested();
				return Task.FromResult(new SendResult { IsSuccess = true });
			});

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => sender.SendAsync(CreateMessage(), cts.Token));
	}

	[Fact]
	public async Task ThrowOperationCanceledWhenConnectionDropsDuringReceive()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var receiver = A.Fake<ITransportReceiver>();
		A.CallTo(() => receiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				cts.Cancel();
				ct.ThrowIfCancellationRequested();
				return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>([]);
			});

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => receiver.ReceiveAsync(10, cts.Token));
	}

	[Fact]
	public async Task ThrowOperationCanceledWhenPreCancelledTokenUsedForSend()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				call.GetArgument<CancellationToken>(1).ThrowIfCancellationRequested();
				return Task.FromResult(new SendResult { IsSuccess = true });
			});

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => sender.SendAsync(CreateMessage(), cts.Token));
	}

	#endregion

	#region Acknowledge/Reject Failure Tests

	[Fact]
	public async Task PropagateExceptionFromAcknowledgeAsync()
	{
		// Arrange
		var receiver = A.Fake<ITransportReceiver>();
		A.CallTo(() => receiver.AcknowledgeAsync(A<TransportReceivedMessage>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Lock expired"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => receiver.AcknowledgeAsync(CreateReceivedMessage(), CancellationToken.None));
	}

	[Fact]
	public async Task PropagateExceptionFromRejectAsync()
	{
		// Arrange
		var receiver = A.Fake<ITransportReceiver>();
		A.CallTo(() => receiver.RejectAsync(
				A<TransportReceivedMessage>._, A<string?>._, A<bool>._, A<CancellationToken>._))
			.ThrowsAsync(new IOException("Connection lost during reject"));

		// Act & Assert
		await Should.ThrowAsync<IOException>(
			() => receiver.RejectAsync(CreateReceivedMessage(), "test failure", requeue: true, CancellationToken.None));
	}

	#endregion

	#region Flush Failure Tests

	[Fact]
	public async Task PropagateExceptionFromFlushAsync()
	{
		// Arrange
		var sender = A.Fake<ITransportSender>();
		A.CallTo(() => sender.FlushAsync(A<CancellationToken>._))
			.ThrowsAsync(new IOException("Flush failed: broken pipe"));

		// Act & Assert
		await Should.ThrowAsync<IOException>(
			() => sender.FlushAsync(CancellationToken.None));
	}

	#endregion
}
