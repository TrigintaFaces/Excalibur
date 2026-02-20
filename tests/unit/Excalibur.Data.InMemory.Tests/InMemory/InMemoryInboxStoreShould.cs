// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryInboxStoreShould : IAsyncDisposable
{
	private readonly InMemoryInboxStore _store;

	public InMemoryInboxStoreShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		_store = new InMemoryInboxStore(options, NullLogger<InMemoryInboxStore>.Instance);
	}

	public ValueTask DisposeAsync() => _store.DisposeAsync();

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

		// Cleanup with zero retention - should remove all processed entries
		var count = await _store.CleanupAsync(TimeSpan.Zero, CancellationToken.None);
		count.ShouldBe(1);

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
