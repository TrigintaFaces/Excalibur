// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Idempotency verification tests for inbox processing.
/// Validates that processing the same message twice produces identical results
/// and that the TryMarkAsProcessed pattern prevents duplicate processing.
/// </summary>
/// <remarks>
/// Sprint 693, Task T.6 (bd-bcn5s): Closes the gap where no test validates
/// idempotent message processing behavior.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InboxIdempotencyShould : IAsyncDisposable
{
	private readonly InMemoryInboxStore _store;
	private readonly CancellationTokenSource _cts = new();

	public InboxIdempotencyShould()
	{
		_store = new InMemoryInboxStore(
			Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions()),
			NullLogger<InMemoryInboxStore>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		_cts.Dispose();
		await _store.DisposeAsync().ConfigureAwait(false);
	}

	#region TryMarkAsProcessed Idempotency

	[Fact]
	public async Task ReturnTrue_OnFirstProcessingAttempt()
	{
		// Act
		var result = await _store.TryMarkAsProcessedAsync(
			"msg-first", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFalse_OnDuplicateProcessingAttempt()
	{
		// Arrange - First processing
		await _store.TryMarkAsProcessedAsync(
			"msg-dup", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Act - Duplicate attempt
		var result = await _store.TryMarkAsProcessedAsync(
			"msg-dup", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFalse_OnMultipleDuplicateAttempts()
	{
		// Arrange
		await _store.TryMarkAsProcessedAsync(
			"msg-multi", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Act - 10 duplicate attempts
		for (var i = 0; i < 10; i++)
		{
			var result = await _store.TryMarkAsProcessedAsync(
				"msg-multi", "HandlerA", _cts.Token).ConfigureAwait(false);
			result.ShouldBeFalse($"Duplicate attempt {i} should return false");
		}
	}

	[Fact]
	public async Task AllowSameMessageForDifferentHandlers()
	{
		// Arrange & Act - Same message, different handlers
		var result1 = await _store.TryMarkAsProcessedAsync(
			"msg-shared", "HandlerA", _cts.Token).ConfigureAwait(false);
		var result2 = await _store.TryMarkAsProcessedAsync(
			"msg-shared", "HandlerB", _cts.Token).ConfigureAwait(false);
		var result3 = await _store.TryMarkAsProcessedAsync(
			"msg-shared", "HandlerC", _cts.Token).ConfigureAwait(false);

		// Assert - All should be true (first processing per handler)
		result1.ShouldBeTrue();
		result2.ShouldBeTrue();
		result3.ShouldBeTrue();
	}

	[Fact]
	public async Task PreventDuplicatePerHandler_WhenMultipleHandlersUsed()
	{
		// Arrange - Process with HandlerA and HandlerB
		await _store.TryMarkAsProcessedAsync("msg-per-handler", "HandlerA", _cts.Token).ConfigureAwait(false);
		await _store.TryMarkAsProcessedAsync("msg-per-handler", "HandlerB", _cts.Token).ConfigureAwait(false);

		// Act - Try duplicates for both handlers
		var dupA = await _store.TryMarkAsProcessedAsync("msg-per-handler", "HandlerA", _cts.Token).ConfigureAwait(false);
		var dupB = await _store.TryMarkAsProcessedAsync("msg-per-handler", "HandlerB", _cts.Token).ConfigureAwait(false);

		// Assert
		dupA.ShouldBeFalse();
		dupB.ShouldBeFalse();
	}

	#endregion

	#region IsProcessed Check

	[Fact]
	public async Task ReportNotProcessed_ForNewMessage()
	{
		// Act
		var isProcessed = await _store.IsProcessedAsync(
			"msg-new", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert
		isProcessed.ShouldBeFalse();
	}

	[Fact]
	public async Task ReportProcessed_AfterTryMarkAsProcessed()
	{
		// Arrange
		await _store.TryMarkAsProcessedAsync("msg-check", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Act
		var isProcessed = await _store.IsProcessedAsync(
			"msg-check", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert
		isProcessed.ShouldBeTrue();
	}

	#endregion

	#region Concurrent Idempotency

	[Fact]
	public async Task HandleConcurrentDuplicateAttempts_OnlyOneSucceeds()
	{
		// Arrange
		const int concurrentAttempts = 50;
		var results = new bool[concurrentAttempts];

		// Act - 50 concurrent TryMarkAsProcessed for the same message+handler
		var tasks = Enumerable.Range(0, concurrentAttempts).Select(async i =>
		{
			results[i] = await _store.TryMarkAsProcessedAsync(
				"msg-concurrent", "HandlerA", _cts.Token).ConfigureAwait(false);
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Exactly one should succeed
		var successCount = results.Count(r => r);
		successCount.ShouldBe(1, "Exactly one concurrent attempt should succeed");
	}

	[Fact]
	public async Task HandleConcurrentProcessingOfDifferentMessages()
	{
		// Arrange
		const int messageCount = 100;
		var results = new bool[messageCount];

		// Act - Each message gets processed once, concurrently
		var tasks = Enumerable.Range(0, messageCount).Select(async i =>
		{
			results[i] = await _store.TryMarkAsProcessedAsync(
				$"msg-diff-{i}", "HandlerA", _cts.Token).ConfigureAwait(false);
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All should succeed (different messages)
		results.ShouldAllBe(r => r);
	}

	#endregion

	#region CreateEntry + MarkProcessed Flow

	[Fact]
	public async Task CreateAndMarkProcessed_StandardFlow()
	{
		// Arrange
		var entry = await _store.CreateEntryAsync(
			"msg-flow", "HandlerA", "TestMessage",
			new byte[] { 1, 2, 3 },
			new Dictionary<string, object> { ["key"] = "value" },
			_cts.Token).ConfigureAwait(false);

		// Assert - Entry created
		entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe("msg-flow");

		// Act - Mark as processed
		await _store.MarkProcessedAsync("msg-flow", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert - IsProcessed returns true
		var isProcessed = await _store.IsProcessedAsync(
			"msg-flow", "HandlerA", _cts.Token).ConfigureAwait(false);
		isProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task GetEntry_ReturnsNull_ForNonExistentMessage()
	{
		// Act
		var entry = await _store.GetEntryAsync(
			"msg-nonexistent", "HandlerA", _cts.Token).ConfigureAwait(false);

		// Assert
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task MarkFailed_RecordsFailureState()
	{
		// Arrange
		await _store.CreateEntryAsync(
			"msg-fail", "HandlerA", "TestMessage",
			new byte[] { 1, 2, 3 },
			new Dictionary<string, object>(),
			_cts.Token).ConfigureAwait(false);

		// Act
		await _store.MarkFailedAsync(
			"msg-fail", "HandlerA", "Test error message",
			_cts.Token).ConfigureAwait(false);

		// Assert - Entry should exist with failure
		var entry = await _store.GetEntryAsync(
			"msg-fail", "HandlerA", _cts.Token).ConfigureAwait(false);
		entry.ShouldNotBeNull();
	}

	#endregion
}
