// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBulkCleanupAdapterDepthShould
{
	private readonly IOutboxStoreAdmin _admin = A.Fake<IOutboxStoreAdmin>();

	[Fact]
	public void ThrowWhenAdminIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboxBulkCleanupAdapter(null!, NullLogger<OutboxBulkCleanupAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboxBulkCleanupAdapter(_admin, null!));
	}

	[Fact]
	public async Task BulkCleanupSentThrowsWhenBatchSizeIsZero()
	{
		// Arrange
		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 0, CancellationToken.None));
	}

	[Fact]
	public async Task BulkCleanupSentThrowsWhenBatchSizeIsNegative()
	{
		// Arrange
		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, -1, CancellationToken.None));
	}

	[Fact]
	public async Task BulkCleanupSentProcessesSingleBatch()
	{
		// Arrange - returns 5 (less than batch size 10), so only one iteration
		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(5));

		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act
		var result = await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None);

		// Assert - returned 5 which is less than batch size 10, so only one batch
		result.ShouldBe(5);
	}

	[Fact]
	public async Task BulkCleanupSentProcessesMultipleBatches()
	{
		// Arrange - batch size 5: first returns 5 (== batch size, loop continues), then 3 (< batch size, stops)
		var callCount = 0;
		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return new ValueTask<int>(callCount == 1 ? 5 : 3);
			});

		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act
		var result = await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 5, CancellationToken.None);

		// Assert
		result.ShouldBe(8); // 5 + 3
	}

	[Fact]
	public async Task BulkCleanupSentReturnsZeroWhenNothingToClean()
	{
		// Arrange
		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(0));

		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act
		var result = await sut.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task BulkCleanupFailedThrowsWhenBatchSizeIsZero()
	{
		// Arrange
		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await sut.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 0, CancellationToken.None));
	}

	[Fact]
	public async Task BulkCleanupFailedReturnsZeroWhenNoFailedMessages()
	{
		// Arrange - GetFailedMessagesAsync returns empty enumerable
		A.CallTo(() => _admin.GetFailedMessagesAsync(A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act
		var result = await sut.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 100, CancellationToken.None);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task BulkCleanupFailedProcessesFailedMessages()
	{
		// Arrange - first call returns 2 messages, second returns empty
		var failedMessages = new List<OutboundMessage>
		{
			new("TypeA", new byte[] { 1 }, "dest-1"),
			new("TypeB", new byte[] { 2 }, "dest-2"),
		};

		var callCount = 0;
		A.CallTo(() => _admin.GetFailedMessagesAsync(A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				IEnumerable<OutboundMessage> result = callCount == 1
					? failedMessages
					: Enumerable.Empty<OutboundMessage>();
				return new ValueTask<IEnumerable<OutboundMessage>>(result);
			});

		A.CallTo(() => _admin.CleanupSentMessagesAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(2));

		var sut = new OutboxBulkCleanupAdapter(_admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		// Act
		var result = await sut.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 10, CancellationToken.None);

		// Assert
		result.ShouldBe(2);
	}
}
