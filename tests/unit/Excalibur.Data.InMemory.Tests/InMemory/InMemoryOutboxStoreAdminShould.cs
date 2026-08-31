// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;
using Excalibur.Outbox.InMemory;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Tests for the <see cref="IOutboxStoreAdmin"/> interface as implemented by <see cref="InMemoryOutboxStore"/>.
/// Verifies the ISP split from S559 -- IOutboxStoreAdmin is a separate sub-interface from IOutboxStore.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
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
		using var store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);

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

		methodNames.ShouldContain("GetAllTenantsFailedMessagesAsync");
		methodNames.ShouldContain("GetAllTenantsScheduledMessagesAsync");
		methodNames.ShouldContain("CleanupAllTenantsSentMessagesAsync");
		methodNames.ShouldContain("GetAllTenantsStatisticsAsync");
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

	#region GetAllTenantsFailedMessagesAsync Tests

	[Fact]
	public async Task GetFailedMessages_EmptyStore_ReturnsEmpty()
	{
		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_NoFailedMessages_ReturnsEmpty()
	{
		// Stage a message but don't fail it
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
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

		var result = await _admin.GetAllTenantsFailedMessagesAsync(0, null, 100, CancellationToken.None)
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

		var result = await _admin.GetAllTenantsFailedMessagesAsync(3, null, 100, CancellationToken.None)
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

		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
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
		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
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
		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, DateTimeOffset.UtcNow.AddSeconds(-1), 100, CancellationToken.None)
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

		var result = (await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
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

		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 2, CancellationToken.None)
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

		var result = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
			.ConfigureAwait(false);
		var resultList = result.ToList();

		resultList.ShouldNotContain(m => m.Id == sentMessage.Id);
		resultList.ShouldContain(m => m.Id == failedMessage.Id);
	}

	#endregion

	#region GetAllTenantsScheduledMessagesAsync Tests

	[Fact]
	public async Task GetScheduledMessages_EmptyStore_ReturnsEmpty()
	{
		var result = await _admin.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetScheduledMessages_NoScheduledMessages_ReturnsEmpty()
	{
		// Stage a non-scheduled message
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await _admin.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
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
		var result = (await _admin.GetAllTenantsScheduledMessagesAsync(
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

		var result = (await _admin.GetAllTenantsScheduledMessagesAsync(
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

		var result = (await _admin.GetAllTenantsScheduledMessagesAsync(
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

		var result = (await _admin.GetAllTenantsScheduledMessagesAsync(
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

		var result = await _admin.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 3, CancellationToken.None)
			.ConfigureAwait(false);

		result.Count().ShouldBe(3);
	}

	#endregion

	#region CleanupAllTenantsSentMessagesAsync Tests

	[Fact]
	public async Task CleanupSentMessages_EmptyStore_ReturnsZero()
	{
		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
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

		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
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
		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(1);

		// Verify the message is actually gone from statistics
		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.SentMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesRecentSentMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Clean up only messages sent before 1 hour ago (our message was just sent)
		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(-1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(0);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
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

		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
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

		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 100, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(1);

		// Failed message should still exist
		var failedMessages = await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
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

		var result = await _admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1), 2, CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(2);

		// 3 should remain
		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.SentMessageCount.ShouldBe(3);
	}

	#endregion

	#region GetAllTenantsStatisticsAsync Tests

	[Fact]
	public async Task GetStatistics_EmptyStore_ReturnsZeroCounts()
	{
		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

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
		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
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

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.StagedMessageCount.ShouldBe(3);
		stats.TotalMessageCount.ShouldBe(3);
	}

	[Fact]
	public async Task GetStatistics_CountsSentMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.SentMessageCount.ShouldBe(1);
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_CountsFailedMessages()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.FailedMessageCount.ShouldBe(1);
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_CountsScheduledMessages()
	{
		var scheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
		await _store.StageMessageAsync(scheduled, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		stats.ScheduledMessageCount.ShouldBe(1);
		// Scheduled messages are also counted as Staged
		stats.StagedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task GetStatistics_TracksOldestUnsentMessageAge()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		_ = stats.OldestUnsentMessageAge.ShouldNotBeNull();
		stats.OldestUnsentMessageAge.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public async Task GetStatistics_TracksOldestFailedMessageAge()
	{
		var message = CreateTestMessage();
		await _store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await _store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

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

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

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

		var stats = await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var str = stats.ToString();

		str.ShouldContain("OutboxStats");
		str.ShouldContain("staged");
	}

	#endregion

	#region Capacity Eviction Tests

	[Fact]
	public async Task StageMessage_WhenCapacityReached_EvictsOldestSentMessageFirst()
	{
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 2,
			DefaultRetentionPeriod = TimeSpan.FromHours(24)
		});
		using var store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
		var admin = (IOutboxStoreAdmin)store;

		var sentMessage = CreateTestMessage("sent-oldest");
		var stagedMessage = CreateTestMessage("staged-keep");
		var newestMessage = CreateTestMessage("newest");

		await store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(newestMessage, CancellationToken.None).ConfigureAwait(false);

		var stats = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.TotalMessageCount.ShouldBe(2);
		stats.SentMessageCount.ShouldBe(0);
		stats.StagedMessageCount.ShouldBe(2);

		var unsent = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		unsent.ShouldContain(m => m.Id == stagedMessage.Id);
		unsent.ShouldContain(m => m.Id == newestMessage.Id);
	}

	[Fact]
	public async Task StageMessage_WhenCapacityReachedAndEveryMessageIsStillOwedDelivery_RefusesToEvictAndThrows()
	{
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 2,
			DefaultRetentionPeriod = TimeSpan.FromHours(24)
		});
		using var store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);

		var oldest = CreateTestMessage("oldest");
		var middle = CreateTestMessage("middle");
		var newest = CreateTestMessage("newest");

		var now = DateTimeOffset.UtcNow;
		oldest.CreatedAt = now.AddMinutes(-3);
		middle.CreatedAt = now.AddMinutes(-2);
		newest.CreatedAt = now.AddMinutes(-1);

		await store.StageMessageAsync(oldest, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(middle, CancellationToken.None).ConfigureAwait(false);

		// Every resident message is Staged, so none is terminal and none may be reclaimed. Evicting the
		// oldest -- which this test used to require -- would deliver it zero times and break at-least-once
		// at its floor, so the store refuses and the staging call fails instead.
		var refusal = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.StageMessageAsync(newest, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		// The remedy is part of the contract: an operator reading this in a log has to learn which knob to
		// turn, so the option name is asserted rather than left to the prose around it.
		refusal.Message.ShouldContain(
			nameof(InMemoryOutboxOptions.MaxMessages),
			Case.Sensitive,
			"the refusal must name the option that raises the ceiling, or it tells an operator nothing actionable");

		// The refusal is not a silent partial write: the two undelivered messages are intact and the
		// rejected one was never admitted.
		var unsent = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		unsent.ShouldContain(m => m.Id == oldest.Id);
		unsent.ShouldContain(m => m.Id == middle.Id);
		unsent.ShouldNotContain(m => m.Id == newest.Id);
	}

	[Fact]
	public async Task StageMessage_WhenCapacityReachedWithOnlyADeadLetteredMessage_EvictsIt()
	{
		// Liveness for the refusal above, on the terminal state its sibling does not cover: the Sent branch
		// is exercised by StageMessage_WhenCapacityReached_EvictsOldestSentMessageFirst, this one binds
		// DeadLettered. Without an arm like this a store that refused EVERY eviction would pass the refusal
		// test and look correct while the capacity ceiling had become permanent.
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 2,
			DefaultRetentionPeriod = TimeSpan.FromHours(24)
		});
		using var store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);

		var deadLettered = CreateTestMessage("dead-lettered");
		var staged = CreateTestMessage("staged-keep");
		var newest = CreateTestMessage("newest");

		await store.StageMessageAsync(deadLettered, CancellationToken.None).ConfigureAwait(false);
		await store.MarkDeadLetteredAsync(deadLettered.Id, "exhausted", CancellationToken.None)
			.ConfigureAwait(false);
		await store.StageMessageAsync(staged, CancellationToken.None).ConfigureAwait(false);

		await Should.NotThrowAsync(async () =>
			await store.StageMessageAsync(newest, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		var unsent = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		unsent.ShouldContain(m => m.Id == staged.Id);
		unsent.ShouldContain(m => m.Id == newest.Id);
		unsent.ShouldNotContain(m => m.Id == deadLettered.Id);
	}

	#endregion

	#region Disposed State Tests

	[Fact]
	public async Task GetFailedMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetAllTenantsFailedMessagesAsync(10, null, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task GetScheduledMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetAllTenantsScheduledMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task CleanupSentMessages_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.CleanupAllTenantsSentMessagesAsync(DateTimeOffset.UtcNow, 100, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task GetStatistics_WhenDisposed_ThrowsObjectDisposedException()
	{
		_store.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false));
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