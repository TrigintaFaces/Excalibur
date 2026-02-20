// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Tests for the <see cref="IOutboxStoreAdmin"/> interface as implemented by <see cref="InMemoryOutboxStore"/>.
/// Verifies the ISP split from S559 -- IOutboxStoreAdmin is a separate sub-interface from IOutboxStore.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryOutboxStoreAdminShould : IDisposable
{
	private readonly InMemoryOutboxStore _store;
	private readonly IOutboxStoreAdmin _admin;

	public InMemoryOutboxStoreAdminShould()
	{
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 10000,
			DefaultRetentionPeriod = TimeSpan.FromHours(24)
		});

		_store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
		_admin = _store;
	}

	public void Dispose()
	{
		_store.Dispose();
	}

	#region Interface Segregation Verification

	[Fact]
	public void InMemoryOutboxStore_ShouldImplementIOutboxStoreAdmin()
	{
		_ = _store.ShouldBeAssignableTo<IOutboxStoreAdmin>();
	}

	[Fact]
	public void InMemoryOutboxStore_ShouldImplementIOutboxStore()
	{
		_ = _store.ShouldBeAssignableTo<IOutboxStore>();
	}

	[Fact]
	public void IOutboxStoreAdmin_ShouldBeSeparateFromIOutboxStore()
	{
		// Verify IOutboxStoreAdmin does not inherit from IOutboxStore
		typeof(IOutboxStoreAdmin).IsAssignableFrom(typeof(IOutboxStore)).ShouldBeFalse(
			"IOutboxStoreAdmin should be a separate interface, not derived from IOutboxStore");
		typeof(IOutboxStore).IsAssignableFrom(typeof(IOutboxStoreAdmin)).ShouldBeFalse(
			"IOutboxStore should not derive from IOutboxStoreAdmin");
	}

	[Fact]
	public void IOutboxStoreAdmin_ShouldHaveFourMethods()
	{
		// Verify the ISP split kept IOutboxStoreAdmin to exactly 4 methods
		var methods = typeof(IOutboxStoreAdmin).GetMethods();
		methods.Length.ShouldBe(4, "IOutboxStoreAdmin should have exactly 4 methods per ISP gate");
	}

	[Fact]
	public void IOutboxStoreAdmin_MethodNames_ShouldMatchContract()
	{
		var methodNames = typeof(IOutboxStoreAdmin).GetMethods().Select(m => m.Name).OrderBy(n => n).ToArray();

		methodNames.ShouldContain("GetFailedMessagesAsync");
		methodNames.ShouldContain("GetScheduledMessagesAsync");
		methodNames.ShouldContain("CleanupSentMessagesAsync");
		methodNames.ShouldContain("GetStatisticsAsync");
	}

	[Fact]
	public void CastToAdmin_FromStoreInstance_ShouldSucceed()
	{
		// The GetService pattern -- cast IOutboxStore to IOutboxStoreAdmin
		IOutboxStore store = _store;
		var admin = store as IOutboxStoreAdmin;

		_ = admin.ShouldNotBeNull("InMemoryOutboxStore should be castable to IOutboxStoreAdmin");
	}

	#endregion

	#region GetFailedMessagesAsync Tests

	[Fact]
	public async Task GetFailedMessages_EmptyStore_ReturnsEmpty()
	{
		var result = await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_NoFailedMessages_ReturnsEmpty()
	{
		// Stage a message but don't fail it
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_WithMaxRetriesZero_ReturnsAllFailed()
	{
		// maxRetries <= 0 should return all failed messages regardless of retry count
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 99, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetFailedMessagesAsync(0, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task GetFailedMessages_ExcludesMessagesAtMaxRetries()
	{
		// maxRetries=3 means only messages with retryCount < 3 should be returned
		var underLimit = CreateTestMessage("under-limit");
		var atLimit = CreateTestMessage("at-limit");
		var overLimit = CreateTestMessage("over-limit");

		await _store.StageMessageAsync(underLimit, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(atLimit, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(overLimit, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkFailedAsync(underLimit.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(atLimit.Id, "Error", 3, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(overLimit.Id, "Error", 5, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetFailedMessagesAsync(3, null, 100, CancellationToken.None)
			.ConfigureAwait(false);
		var resultList = result.ToList();

		resultList.ShouldContain(m => m.Id == underLimit.Id);
		resultList.ShouldNotContain(m => m.Id == atLimit.Id);
		resultList.ShouldNotContain(m => m.Id == overLimit.Id);
	}

	[Fact]
	public async Task GetFailedMessages_WithNullOlderThan_ReturnsAllFailed()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task GetFailedMessages_WithFutureOlderThan_ReturnsAll()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// olderThan in the future should include recently-failed messages
		var result = await _admin.GetFailedMessagesAsync(10, DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task GetFailedMessages_WithPastOlderThan_ExcludesRecentFailures()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// olderThan in the past should exclude recently-failed messages
		var result = await _admin.GetFailedMessagesAsync(10, DateTimeOffset.UtcNow.AddSeconds(-1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_OrdersByRetryCountThenLastAttempt()
	{
		var msg1 = CreateTestMessage("msg-1");
		var msg2 = CreateTestMessage("msg-2");
		var msg3 = CreateTestMessage("msg-3");

		await _store.StageMessageAsync(msg1, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(msg2, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(msg3, CancellationToken.None).ConfigureAwait(false);

		// Fail with different retry counts
		await _store.MarkFailedAsync(msg1.Id, "Error", 3, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(msg2.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(msg3.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);

		var result = (await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		// Should be sorted by retry count ascending
		result[0].RetryCount.ShouldBeLessThanOrEqualTo(result[1].RetryCount);
		result[1].RetryCount.ShouldBeLessThanOrEqualTo(result[2].RetryCount);
	}

	[Fact]
	public async Task GetFailedMessages_RespectsBatchSize()
	{
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		}

		var result = await _admin.GetFailedMessagesAsync(10, null, 2, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(2);
	}

	[Fact]
	public async Task GetFailedMessages_ExcludesSentMessages()
	{
		var sentMessage = CreateTestMessage();
		var failedMessage = CreateTestMessage();

		await _store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);
		var resultList = result.ToList();

		resultList.ShouldNotContain(m => m.Id == sentMessage.Id);
		resultList.ShouldContain(m => m.Id == failedMessage.Id);
	}

	#endregion

	#region GetScheduledMessagesAsync Tests

	[Fact]
	public async Task GetScheduledMessages_EmptyStore_ReturnsEmpty()
	{
		var result = await _admin.GetScheduledMessagesAsync(DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetScheduledMessages_NoScheduledMessages_ReturnsEmpty()
	{
		// Stage a non-scheduled message
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetScheduledMessagesAsync(DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetScheduledMessages_ReturnsOnlyScheduledWithinThreshold()
	{
		var pastScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-30));
		var soonScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(30));
		var farFutureScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddDays(7));

		await _store.StageMessageAsync(pastScheduled, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(soonScheduled, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(farFutureScheduled, CancellationToken.None).ConfigureAwait(false);

		// Query for messages scheduled before 1 hour from now
		var result = (await _admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		result.ShouldContain(m => m.Id == pastScheduled.Id);
		result.ShouldContain(m => m.Id == soonScheduled.Id);
		result.ShouldNotContain(m => m.Id == farFutureScheduled.Id);
	}

	[Fact]
	public async Task GetScheduledMessages_ExcludesNonScheduledMessages()
	{
		var scheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(5));
		var immediate = CreateTestMessage(); // No ScheduledAt

		await _store.StageMessageAsync(scheduled, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(immediate, CancellationToken.None).ConfigureAwait(false);

		var result = (await _admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		result.ShouldContain(m => m.Id == scheduled.Id);
		result.ShouldNotContain(m => m.Id == immediate.Id);
	}

	[Fact]
	public async Task GetScheduledMessages_ExcludesSentAndFailedMessages()
	{
		var scheduledAndSent = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-5));
		var scheduledAndFailed = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-5));
		var scheduledPending = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-5));

		await _store.StageMessageAsync(scheduledAndSent, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(scheduledAndFailed, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(scheduledPending, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkSentAsync(scheduledAndSent.Id, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(scheduledAndFailed.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var result = (await _admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		result.ShouldNotContain(m => m.Id == scheduledAndSent.Id);
		result.ShouldNotContain(m => m.Id == scheduledAndFailed.Id);
		result.ShouldContain(m => m.Id == scheduledPending.Id);
	}

	[Fact]
	public async Task GetScheduledMessages_OrdersByScheduledAtAscending()
	{
		var later = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(30));
		var earlier = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(10));
		var middle = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(20));

		await _store.StageMessageAsync(later, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(earlier, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(middle, CancellationToken.None).ConfigureAwait(false);

		var result = (await _admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		result.Count.ShouldBe(3);
		result[0].ScheduledAt!.Value.ShouldBeLessThanOrEqualTo(result[1].ScheduledAt!.Value);
		result[1].ScheduledAt!.Value.ShouldBeLessThanOrEqualTo(result[2].ScheduledAt!.Value);
	}

	[Fact]
	public async Task GetScheduledMessages_RespectsBatchSize()
	{
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(i + 1));
			await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		var result = await _admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 3, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(3);
	}

	#endregion

	#region CleanupSentMessagesAsync Tests

	[Fact]
	public async Task CleanupSentMessages_EmptyStore_ReturnsZero()
	{
		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_NoSentMessages_ReturnsZero()
	{
		// Stage but don't send
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_RemovesSentBeforeThreshold()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Clean up everything sent before 1 hour from now (includes all just-sent)
		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(1);

		// Verify the message is actually gone from statistics
		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.SentMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesRecentSentMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Clean up only messages sent before 1 hour ago (our message was just sent)
		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(-1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(0);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.SentMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesStagedMessages()
	{
		var staged = CreateTestMessage();
		var sent = CreateTestMessage();

		await _store.StageMessageAsync(staged, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(sent, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(sent.Id, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(1);

		// Staged message should still be retrievable
		var unsent = await _store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldContain(m => m.Id == staged.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesFailedMessages()
	{
		var failed = CreateTestMessage();
		var sent = CreateTestMessage();

		await _store.StageMessageAsync(failed, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(sent, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkFailedAsync(failed.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(sent.Id, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(1);

		// Failed message should still exist
		var failedMessages = await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);
		failedMessages.ShouldContain(m => m.Id == failed.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_RespectsBatchSize()
	{
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}

		var result = await _admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 2, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(2);

		// 3 should remain
		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.SentMessageCount.ShouldBe(3);
	}

	#endregion

	#region GetStatisticsAsync Tests

	[Fact]
	public async Task GetStatistics_EmptyStore_ReturnsZeroCounts()
	{
		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(0);
		stats.SendingMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
		stats.ScheduledMessageCount.ShouldBe(0);
		stats.TotalMessageCount.ShouldBe(0);
		stats.OldestUnsentMessageAge.ShouldBeNull();
		stats.OldestFailedMessageAge.ShouldBeNull();
	}

	[Fact]
	public async Task GetStatistics_CapturedAtIsSet()
	{
		var before = DateTimeOffset.UtcNow;
		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var after = DateTimeOffset.UtcNow;

		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(before);
		stats.CapturedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public async Task GetStatistics_CountsStagedMessages()
	{
		for (int i = 0; i < 3; i++)
		{
			await _store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(3);
		stats.TotalMessageCount.ShouldBe(3);
	}

	[Fact]
	public async Task GetStatistics_CountsSentMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.SentMessageCount.ShouldBe(1);
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_CountsFailedMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.FailedMessageCount.ShouldBe(1);
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_CountsScheduledMessages()
	{
		var scheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
		await _store.StageMessageAsync(scheduled, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.ScheduledMessageCount.ShouldBe(1);
		// Scheduled messages are also counted as Staged
		stats.StagedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task GetStatistics_TracksOldestUnsentMessageAge()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		_ = stats.OldestUnsentMessageAge.ShouldNotBeNull();
		stats.OldestUnsentMessageAge.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task GetStatistics_TracksOldestFailedMessageAge()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		_ = stats.OldestFailedMessageAge.ShouldNotBeNull();
		stats.OldestFailedMessageAge.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task GetStatistics_AllStates_AggregatesCorrectly()
	{
		var staged1 = CreateTestMessage();
		var staged2 = CreateTestMessage();
		var sent1 = CreateTestMessage();
		var sent2 = CreateTestMessage();
		var failed1 = CreateTestMessage();
		var scheduled1 = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(1));

		await _store.StageMessageAsync(staged1, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(staged2, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(sent1, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(sent2, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(failed1, CancellationToken.None).ConfigureAwait(false);
		await _store.StageMessageAsync(scheduled1, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkSentAsync(sent1.Id, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(sent2.Id, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(failed1.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(3); // 2 immediate + 1 scheduled
		stats.SentMessageCount.ShouldBe(2);
		stats.FailedMessageCount.ShouldBe(1);
		stats.ScheduledMessageCount.ShouldBe(1);
		// TotalMessageCount = Staged(3) + Sending(0) + Sent(2) + Failed(1) + Scheduled(1) = 7
		// Note: scheduled messages are counted in BOTH StagedMessageCount and ScheduledMessageCount
		stats.TotalMessageCount.ShouldBe(7);
	}

	[Fact]
	public async Task GetStatistics_ToString_ContainsSummary()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var str = stats.ToString();

		str.ShouldContain("OutboxStats");
		str.ShouldContain("staged");
	}

	#endregion

	#region Disposed State Tests

	[Fact]
	public async Task GetFailedMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetFailedMessagesAsync(10, null, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task GetScheduledMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetScheduledMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task CleanupSentMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.CleanupSentMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task GetStatistics_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false));
	}

	#endregion

	#region Helper Methods

	private static OutboundMessage CreateTestMessage(
		string? id = null,
		string? messageType = null,
		DateTimeOffset? scheduledAt = null)
	{
		return new OutboundMessage(
			messageType ?? "Test.MessageType",
			"test-payload"u8.ToArray(),
			"test-queue")
		{
			Id = id ?? Guid.NewGuid().ToString(),
			ScheduledAt = scheduledAt
		};
	}

	#endregion
}
