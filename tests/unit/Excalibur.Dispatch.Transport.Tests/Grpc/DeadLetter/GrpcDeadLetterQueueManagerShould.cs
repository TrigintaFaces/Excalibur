// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc.DeadLetter;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Grpc.DeadLetter;

/// <summary>
/// Unit tests for <see cref="GrpcDeadLetterQueueManager"/>.
/// Sprint 697 T.33: gRPC transport test coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcDeadLetterQueueManagerShould
{
	private readonly GrpcDeadLetterQueueManager _sut;

	public GrpcDeadLetterQueueManagerShould()
	{
		_sut = new GrpcDeadLetterQueueManager(
			NullLogger<GrpcDeadLetterQueueManager>.Instance);
	}

	#region MoveToDeadLetterAsync

	[Fact]
	public async Task MoveToDeadLetterAsync_ReturnMessageId()
	{
		// Arrange
		var message = new TransportMessage { Id = "msg-123" };

		// Act
		var result = await _sut.MoveToDeadLetterAsync(
			message, "test-reason", null, CancellationToken.None);

		// Assert
		result.ShouldBe("msg-123");
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_GenerateIdWhenNull()
	{
		// Arrange
		var message = new TransportMessage { Id = null };

		// Act
		var result = await _sut.MoveToDeadLetterAsync(
			message, "test-reason", null, CancellationToken.None);

		// Assert
		result.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowOnNullMessage()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MoveToDeadLetterAsync(null!, "reason", null, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_ThrowOnNullReason()
	{
		// Arrange
		var message = new TransportMessage();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.MoveToDeadLetterAsync(message, null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_StoreMessageForRetrieval()
	{
		// Arrange
		var message = new TransportMessage { Id = "msg-stored" };

		// Act
		await _sut.MoveToDeadLetterAsync(message, "failed", null, CancellationToken.None);
		var messages = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		messages.Count.ShouldBe(1);
		messages[0].OriginalMessage.Id.ShouldBe("msg-stored");
		messages[0].Reason.ShouldBe("failed");
	}

	[Fact]
	public async Task MoveToDeadLetterAsync_StoreExceptionDetails()
	{
		// Arrange
		var message = new TransportMessage { Id = "msg-err" };
		var exception = new InvalidOperationException("test error");

		// Act
		await _sut.MoveToDeadLetterAsync(message, "exception", exception, CancellationToken.None);
		var messages = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		messages[0].Exception.ShouldBe(exception);
	}

	#endregion

	#region Capacity Enforcement

	[Fact]
	public async Task EvictOldestWhenCapacityExceeded()
	{
		// Arrange -- use small capacity
		var sut = new GrpcDeadLetterQueueManager(
			NullLogger<GrpcDeadLetterQueueManager>.Instance,
			maxCapacity: 3);

		// Act -- add 4 messages (exceeds capacity of 3)
		for (var i = 1; i <= 4; i++)
		{
			await sut.MoveToDeadLetterAsync(
				new TransportMessage { Id = $"msg-{i}" },
				$"reason-{i}", null, CancellationToken.None);
		}

		var messages = await sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert -- oldest message should be evicted
		messages.Count.ShouldBe(3);
		messages.ShouldNotContain(m => m.OriginalMessage.Id == "msg-1");
	}

	#endregion

	#region GetDeadLetterMessagesAsync

	[Fact]
	public async Task GetDeadLetterMessagesAsync_ReturnEmptyWhenNoMessages()
	{
		// Act
		var messages = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDeadLetterMessagesAsync_LimitResults()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			await _sut.MoveToDeadLetterAsync(
				new TransportMessage { Id = $"msg-{i}" },
				"reason", null, CancellationToken.None);
		}

		// Act
		var messages = await _sut.GetDeadLetterMessagesAsync(3, CancellationToken.None);

		// Assert
		messages.Count.ShouldBe(3);
	}

	#endregion

	#region ReprocessDeadLetterMessagesAsync

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ReturnSuccessCount()
	{
		// Arrange
		var dlqMessages = new[]
		{
			new DeadLetterMessage { OriginalMessage = new TransportMessage { Id = "1" } },
			new DeadLetterMessage { OriginalMessage = new TransportMessage { Id = "2" } },
		};

		var options = new ReprocessOptions();

		// Act
		var result = await _sut.ReprocessDeadLetterMessagesAsync(
			dlqMessages, options, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(2);
		result.ProcessingTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowOnNullMessages()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReprocessDeadLetterMessagesAsync(null!, new ReprocessOptions(), CancellationToken.None));
	}

	[Fact]
	public async Task ReprocessDeadLetterMessagesAsync_ThrowOnNullOptions()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReprocessDeadLetterMessagesAsync([], null!, CancellationToken.None));
	}

	#endregion

	#region GetStatisticsAsync

	[Fact]
	public async Task GetStatisticsAsync_ReturnZeroCountWhenEmpty()
	{
		// Act
		var stats = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.MessageCount.ShouldBe(0);
		stats.AverageDeliveryAttempts.ShouldBe(0);
		stats.OldestMessageAge.ShouldBe(TimeSpan.Zero);
		stats.NewestMessageAge.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public async Task GetStatisticsAsync_ReturnAccurateCountAfterAdding()
	{
		// Arrange
		await _sut.MoveToDeadLetterAsync(
			new TransportMessage { Id = "a" }, "reason-a", null, CancellationToken.None);
		await _sut.MoveToDeadLetterAsync(
			new TransportMessage { Id = "b" }, "reason-b", null, CancellationToken.None);

		// Act
		var stats = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.MessageCount.ShouldBe(2);
		stats.ReasonBreakdown.Count.ShouldBe(2);
		stats.ReasonBreakdown["reason-a"].ShouldBe(1);
		stats.ReasonBreakdown["reason-b"].ShouldBe(1);
	}

	#endregion

	#region PurgeDeadLetterQueueAsync

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_ReturnZeroWhenEmpty()
	{
		// Act
		var purged = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purged.ShouldBe(0);
	}

	[Fact]
	public async Task PurgeDeadLetterQueueAsync_RemoveAllMessages()
	{
		// Arrange
		for (var i = 0; i < 3; i++)
		{
			await _sut.MoveToDeadLetterAsync(
				new TransportMessage { Id = $"msg-{i}" },
				"reason", null, CancellationToken.None);
		}

		// Act
		var purged = await _sut.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		purged.ShouldBe(3);

		var remaining = await _sut.GetDeadLetterMessagesAsync(10, CancellationToken.None);
		remaining.ShouldBeEmpty();
	}

	#endregion
}
