// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.InMemory;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InMemoryInboxStoreShould : IAsyncDisposable
{
	private readonly InMemoryInboxStore _store;

	public InMemoryInboxStoreShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		_store = new InMemoryInboxStore(options, NullLogger<InMemoryInboxStore>.Instance);
	}

	public ValueTask DisposeAsync() => _store.DisposeAsync();

	// Liskov L3 durability/fault-model postcondition (testing-patterns §3 safety∧liveness):
	//   LIVENESS -- a mark that CAN be recorded completes AND is durably observable (GetEntryAsync reads Processed).
	//   SAFETY   -- a mark that CANNOT be recorded (no such entry) THROWS; it never returns successfully as a silent
	//               no-op. A silent no-op would let a redelivery re-process, defeating the inbox's whole purpose.
	// RED if MarkProcessedAsync were changed to swallow the missing-entry case and return without throwing.
	[Fact]
	public async Task MarkProcessed_durably_records_when_recordable_and_throws_when_not()
	{
		const string messageId = "msg-durability";
		const string handlerType = "TestHandler";

		// LIVENESS: an existing entry is durably marked Processed and the transition is observable.
		_ = await _store.CreateEntryAsync(
			messageId, handlerType, "TestMessageType", [1, 2, 3],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None);

		await _store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None);

		var persisted = await _store.GetEntryAsync(messageId, handlerType, CancellationToken.None);
		persisted.ShouldNotBeNull();
		persisted!.Status.ShouldBe(
			InboxStatus.Processed,
			"a completed MarkProcessedAsync guarantees the dedup marker is durably recorded and observable");

		// SAFETY: marking a message with no inbox entry cannot be recorded, so it MUST throw -- not silently succeed.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _store.MarkProcessedAsync("no-such-message", handlerType, CancellationToken.None));
	}

	// L3 for the atomic first-writer path: the bool decision is only returned once durably persisted -- true means
	// the caller is the recorded first writer, and a subsequent call sees that durable record and returns false.
	[Fact]
	public async Task TryMarkAsProcessed_returns_a_durably_backed_first_writer_decision()
	{
		const string messageId = "msg-atomic-durability";
		const string handlerType = "TestHandler";

		(await _store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None))
			.ShouldBeTrue("the first writer wins and the claim is durably recorded");

		(await _store.IsProcessedAsync(messageId, handlerType, CancellationToken.None))
			.ShouldBeTrue("the true result must be backed by a persisted dedup record, observable immediately");

		(await _store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None))
			.ShouldBeFalse("a second writer observes the durable record and is denied -- no double processing");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new InMemoryInboxStore(null!, NullLogger<InMemoryInboxStore>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		Should.Throw<ArgumentNullException>(
			() => new InMemoryInboxStore(options, null!));
	}

	// d2afxn: a Failed entry is RE-ADMITTABLE on redelivery (retry) — mirrors the lease-store CAS predicate.
	// RED on the pre-fix InMemory predicate (absent | Received | expired-Processing) which denies Failed.
	[Fact]
	public async Task Readmit_and_retry_a_failed_entry_on_redelivery()
	{
		var lease = TimeSpan.FromSeconds(30);
		const string messageId = "msg-lease-failed-readmit";
		const string handlerType = "TestHandler";

		(await _store.TryClaimAsync(messageId, handlerType, lease, CancellationToken.None))
			.ShouldBeTrue("the initial claim acquires the lease");
		await _store.MarkFailedAsync(messageId, handlerType, "handler boom", CancellationToken.None);

		var afterFail = await _store.GetEntryAsync(messageId, handlerType, CancellationToken.None);
		afterFail.ShouldNotBeNull();
		afterFail!.Status.ShouldBe(InboxStatus.Failed);

		(await _store.TryClaimAsync(messageId, handlerType, lease, CancellationToken.None))
			.ShouldBeTrue("a Failed entry MUST be re-admittable on redelivery (at-least-once + idempotent-handler contract)");

		var afterReclaim = await _store.GetEntryAsync(messageId, handlerType, CancellationToken.None);
		afterReclaim.ShouldNotBeNull();
		afterReclaim!.Status.ShouldBe(InboxStatus.Processing);

		// d2afxn monotonic-RetryCount guarantee: re-admit PRESERVES retry history (never resets to 0), and
		// RetryCount increments exactly once per failed attempt at the shared finalize (MarkFailed). After the
		// FIRST fail RetryCount is 1; the re-admit above carried it forward (still 1); a SECOND failed attempt
		// must therefore land at 2 — not reset to 1. RED if the re-admit resets RetryCount to 0.
		afterReclaim.RetryCount.ShouldBe(1, "the re-admit must preserve the retry count from the first failed attempt, never reset it");

		await _store.MarkFailedAsync(messageId, handlerType, "handler boom again", CancellationToken.None);

		var afterSecondFail = await _store.GetEntryAsync(messageId, handlerType, CancellationToken.None);
		afterSecondFail.ShouldNotBeNull();
		afterSecondFail!.Status.ShouldBe(InboxStatus.Failed);
		afterSecondFail.RetryCount.ShouldBe(
			2,
			"RetryCount MUST be monotonic across re-admits (2 failed attempts => 2), not reset to 0/1 on re-admittance");
	}

	[Fact]
	public async Task CreateEntry_ThrowsWhenMessageIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.CreateEntryAsync(null!, "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntry_ThrowsWhenHandlerTypeIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.CreateEntryAsync("msg-1", null!, "type", [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntry_ThrowsWhenMessageTypeIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.CreateEntryAsync("msg-1", "handler", null!, [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntry_ThrowsWhenPayloadIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.CreateEntryAsync("msg-1", "handler", "type", null!, new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntry_ThrowsWhenMetadataIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.CreateEntryAsync("msg-1", "handler", "type", [], null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CreateEntry_ReturnsEntry()
	{
		var entry = await _store.CreateEntryAsync("msg-1", "handler", "TestMessage", [1, 2, 3],
			new Dictionary<string, object>(), CancellationToken.None);

		entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe("msg-1");
		entry.HandlerType.ShouldBe("handler");
		entry.MessageType.ShouldBe("TestMessage");
	}

	[Fact]
	public async Task CreateEntry_ThrowsForDuplicateKey()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		await Should.ThrowAsync<InvalidOperationException>(
			() => _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkProcessed_ThrowsWhenMessageIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.MarkProcessedAsync(null!, "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkProcessed_ThrowsWhenHandlerTypeIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.MarkProcessedAsync("msg-1", null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkProcessed_ThrowsWhenEntryNotFound()
	{
		await Should.ThrowAsync<InvalidOperationException>(
			() => _store.MarkProcessedAsync("nonexistent", "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkProcessed_SetsStatusToProcessed()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.MarkProcessedAsync("msg-1", "handler", CancellationToken.None);

		var isProcessed = await _store.IsProcessedAsync("msg-1", "handler", CancellationToken.None);
		isProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkProcessed_ThrowsWhenAlreadyProcessed()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.MarkProcessedAsync("msg-1", "handler", CancellationToken.None);

		await Should.ThrowAsync<InvalidOperationException>(
			() => _store.MarkProcessedAsync("msg-1", "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task IsProcessed_ReturnsFalseForUnknownEntry()
	{
		var result = await _store.IsProcessedAsync("nonexistent", "handler", CancellationToken.None);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task IsProcessed_ThrowsWhenMessageIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _store.IsProcessedAsync(null!, "handler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task GetEntry_ReturnsNullForUnknownEntry()
	{
		var entry = await _store.GetEntryAsync("nonexistent", "handler", CancellationToken.None);
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task GetEntry_ReturnsExistingEntry()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		var entry = await _store.GetEntryAsync("msg-1", "handler", CancellationToken.None);
		entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe("msg-1");
	}

	[Fact]
	public async Task MarkFailed_ThrowsForUnknownEntry()
	{
		await Should.ThrowAsync<InvalidOperationException>(
			() => _store.MarkFailedAsync("nonexistent", "handler", "error", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task MarkFailed_SetsStatusToFailed()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.MarkFailedAsync("msg-1", "handler", "some error", CancellationToken.None);

		var entry = await _store.GetEntryAsync("msg-1", "handler", CancellationToken.None);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);
	}

	[Fact]
	public async Task GetStatistics_ReturnsCorrectCounts()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.CreateEntryAsync("msg-2", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.MarkProcessedAsync("msg-1", "handler", CancellationToken.None);

		var stats = await _store.GetStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(2);
		stats.ProcessedEntries.ShouldBe(1);
	}

	[Fact]
	public async Task GetAllEntries_ReturnsAllEntries()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.CreateEntryAsync("msg-2", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		var entries = await _store.GetAllEntriesAsync(CancellationToken.None);
		entries.Count().ShouldBe(2);
	}

	[Fact]
	public async Task TryMarkAsProcessed_ReturnsTrueForFirstCall()
	{
		var result = await _store.TryMarkAsProcessedAsync("msg-1", "handler", CancellationToken.None);
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task TryMarkAsProcessed_ReturnsFalseForDuplicate()
	{
		await _store.TryMarkAsProcessedAsync("msg-1", "handler", CancellationToken.None);
		var result = await _store.TryMarkAsProcessedAsync("msg-1", "handler", CancellationToken.None);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Cleanup_RemovesProcessedEntries()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await _store.MarkProcessedAsync("msg-1", "handler", CancellationToken.None);

		// Zero retention removes all processed entries at or before the cleanup instant.
		// Poll until the entry ages past the provider's cleanup cutoff to avoid same-tick flakiness.
		var removed = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => await _store.CleanupAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false) > 0,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
			TimeSpan.FromMilliseconds(20));
		removed.ShouldBeTrue();

		var entry = await _store.GetEntryAsync("msg-1", "handler", CancellationToken.None);
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task Dispose_ClearsAllEntries()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		_store.Dispose();

		// After dispose, operations should throw ObjectDisposedException
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _store.GetAllEntriesAsync(CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DisposeAsync_ClearsAllEntries()
	{
		await _store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		await _store.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => _store.GetAllEntriesAsync(CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task CapacityLimit_EvictsOldestEntry()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions { MaxEntries = 2 });
		await using var store = new InMemoryInboxStore(options, NullLogger<InMemoryInboxStore>.Instance);

		await store.CreateEntryAsync("msg-1", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);
		await store.CreateEntryAsync("msg-2", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		// Adding a third entry should evict the oldest
		await store.CreateEntryAsync("msg-3", "handler", "type", [], new Dictionary<string, object>(), CancellationToken.None);

		var stats = await store.GetStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(2);
	}
}
