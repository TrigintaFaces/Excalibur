// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Outbox;

[Trait("Category", "Unit")]
public class OutboxBulkCleanupAdapterFunctionalShould
{
	private static readonly byte[] TestPayload = [0x01];

	[Fact]
	public void Constructor_WithNullAdmin_ShouldThrow()
	{
		var logger = NullLogger<OutboxBulkCleanupAdapter>.Instance;
		Should.Throw<ArgumentNullException>(() => new OutboxBulkCleanupAdapter(null!, logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ShouldThrow()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		Should.Throw<ArgumentNullException>(() => new OutboxBulkCleanupAdapter(admin, null!));
	}

	[Fact]
	public async Task BulkCleanupSentMessages_WithBatchSizeZero_ShouldThrow()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => adapter.BulkCleanupSentMessagesAsync(DateTimeOffset.UtcNow, 0, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task BulkCleanupSentMessages_WithNoMessages_ShouldReturnZero()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		A.CallTo(() => admin.CleanupSentMessagesAsync(
				A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(0));

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupSentMessagesAsync(
			DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(0);
	}

	[Fact]
	public async Task BulkCleanupSentMessages_SingleBatch_ShouldReturnDeletedCount()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		// Return 50 (less than batch size 100) indicating single batch
		A.CallTo(() => admin.CleanupSentMessagesAsync(
				A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(50));

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupSentMessagesAsync(
			DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(50);
		A.CallTo(() => admin.CleanupSentMessagesAsync(
			A<DateTimeOffset>._, 100, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task BulkCleanupSentMessages_MultipleBatches_ShouldIterateUntilIncomplete()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		var callCount = 0;
		A.CallTo(() => admin.CleanupSentMessagesAsync(
				A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				// Return full batch (100) for first 3 calls, then partial (30)
				return new ValueTask<int>(callCount <= 3 ? 100 : 30);
			});

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupSentMessagesAsync(
			DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(330); // 100 + 100 + 100 + 30
		A.CallTo(() => admin.CleanupSentMessagesAsync(
			A<DateTimeOffset>._, 100, A<CancellationToken>._)).MustHaveHappened(4, Times.Exactly);
	}

	[Fact]
	public async Task BulkCleanupFailedMessages_WithBatchSizeZero_ShouldThrow()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => adapter.BulkCleanupFailedMessagesAsync(3, DateTimeOffset.UtcNow, 0, CancellationToken.None).AsTask())
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task BulkCleanupFailedMessages_WithNoMessages_ShouldReturnZero()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		A.CallTo(() => admin.GetFailedMessagesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>()));

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupFailedMessagesAsync(
			3, DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(0);
	}

	[Fact]
	public async Task BulkCleanupFailedMessages_SingleBatch_ShouldProcessAll()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		var failedMessages = CreateOutboundMessages(30);

		A.CallTo(() => admin.GetFailedMessagesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(failedMessages));

		A.CallTo(() => admin.CleanupSentMessagesAsync(
				A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<int>(30));

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupFailedMessagesAsync(
			3, DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(30);
	}

	[Fact]
	public async Task BulkCleanupFailedMessages_MultipleBatches_ShouldIterateUntilNoMore()
	{
		var admin = A.Fake<IOutboxStoreAdmin>();
		var firstBatch = CreateOutboundMessages(100);
		var secondBatch = CreateOutboundMessages(40);

		var getCallCount = 0;
		A.CallTo(() => admin.GetFailedMessagesAsync(
				A<int>._, A<DateTimeOffset?>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				getCallCount++;
				return getCallCount switch
				{
					1 => new ValueTask<IEnumerable<OutboundMessage>>((IEnumerable<OutboundMessage>)firstBatch),
					2 => new ValueTask<IEnumerable<OutboundMessage>>((IEnumerable<OutboundMessage>)secondBatch),
					_ => new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>())
				};
			});

		A.CallTo(() => admin.CleanupSentMessagesAsync(
				A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(call => new ValueTask<int>((int)call.Arguments[1]!));

		var adapter = new OutboxBulkCleanupAdapter(admin, NullLogger<OutboxBulkCleanupAdapter>.Instance);

		var result = await adapter.BulkCleanupFailedMessagesAsync(
			3, DateTimeOffset.UtcNow, 100, CancellationToken.None).ConfigureAwait(true);

		result.ShouldBe(140); // 100 + 40
	}

	private static List<OutboundMessage> CreateOutboundMessages(int count)
	{
		return Enumerable.Range(0, count)
			.Select(_ => new OutboundMessage("TestType", TestPayload, "test-dest"))
			.ToList();
	}
}

