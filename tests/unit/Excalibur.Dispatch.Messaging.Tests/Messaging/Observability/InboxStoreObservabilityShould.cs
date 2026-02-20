// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data.InMemory.Inbox;

namespace Excalibur.Dispatch.Tests.Messaging.Observability;

/// <summary>
///     Observability validation tests for the <see cref="InMemoryInboxStore" /> class.
/// </summary>
[Collection("Observability Tests")]
[Trait("Category", "Unit")]
public sealed class InboxStoreObservabilityShould : IDisposable
{
	private const string TestHandlerType = "TestHandler";
	private readonly InMemoryInboxStore _store;
	private readonly OpenTelemetryTestFixture _otelFixture;

	public InboxStoreObservabilityShould()
	{
		// IMPORTANT: Create fixture FIRST to register ActivityListener before any ActivitySource usage
		_otelFixture = new OpenTelemetryTestFixture();

		var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryInboxStore>.Instance;
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		_store = new InMemoryInboxStore(options, logger);
	}

	[Fact]
	public async Task CreateEntrySuccessfully()
	{
		// Arrange
		var messageId = "test-message";
		var messageType = "TestMessage";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object> { ["test"] = "value" };

		// Act
		var entry = await _store.CreateEntryAsync(messageId, TestHandlerType, messageType, payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);
		entry.MessageType.ShouldBe(messageType);
		entry.HandlerType.ShouldBe(TestHandlerType);
		entry.Status.ShouldBe(Excalibur.Dispatch.Abstractions.InboxStatus.Received);
		entry.Payload.ShouldBe(payload);
		entry.Metadata.ShouldContainKey("test");
		entry.Metadata["test"].ShouldBe("value");
	}

	[Fact]
	public async Task MarkProcessedSuccessfully()
	{
		// Arrange
		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		_ = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Act
		await _store.MarkProcessedAsync(messageId, TestHandlerType, CancellationToken.None);

		// Assert
		var statistics = await _store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		statistics.ProcessedEntries.ShouldBe(1);
		statistics.PendingEntries.ShouldBe(0);
		statistics.TotalEntries.ShouldBe(1);

		var entry = await _store.GetEntryAsync(messageId, TestHandlerType, CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(Excalibur.Dispatch.Abstractions.InboxStatus.Processed);
		_ = entry.ProcessedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task MarkFailedSuccessfully()
	{
		// Arrange
		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();
		var error = "Test error";

		_ = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Act
		await _store.MarkFailedAsync(messageId, TestHandlerType, error, CancellationToken.None);

		// Assert
		var statistics = await _store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		statistics.FailedEntries.ShouldBe(1);
		statistics.PendingEntries.ShouldBe(0);
		statistics.TotalEntries.ShouldBe(1);

		var entry = await _store.GetEntryAsync(messageId, TestHandlerType, CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(Excalibur.Dispatch.Abstractions.InboxStatus.Failed);
		entry.LastError.ShouldBe(error);
	}

	[Fact]
	public async Task CleanupExpiredEntries()
	{
		// Arrange
		var options = new InMemoryInboxOptions { RetentionPeriod = TimeSpan.FromMilliseconds(1) };
		var store = new InMemoryInboxStore(
			Microsoft.Extensions.Options.Options.Create(options),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryInboxStore>.Instance);

		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		_ = await store.CreateEntryAsync("expired-message", TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync("expired-message", TestHandlerType, CancellationToken.None);

		await Task.Delay(10).ConfigureAwait(false); // Wait for expiry

		// Act
		var cleaned = await store.CleanupAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None).ConfigureAwait(false);

		// Assert
		cleaned.ShouldBeGreaterThanOrEqualTo(1);

		var entries = await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		entries.ShouldBeEmpty();

		store.Dispose();
	}

	[Fact]
	public async Task PreserveCorrelationIdInMetadata()
	{
		// Arrange
		var correlationId = Guid.NewGuid().ToString();
		using var parentActivity = new Activity("parent");
		_ = parentActivity.SetTag("correlation.id", correlationId);
		_ = parentActivity.Start();

		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object> { ["CorrelationId"] = correlationId };

		// Act
		var entry = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.Metadata.ShouldContainKey("CorrelationId");
		entry.Metadata["CorrelationId"].ShouldBe(correlationId);
	}

	[Fact]
	public async Task TrackStatisticsAcrossMultipleOperations()
	{
		// Arrange
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - create multiple entries with different statuses
		_ = await _store.CreateEntryAsync("msg-1", TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		_ = await _store.CreateEntryAsync("msg-2", TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		_ = await _store.CreateEntryAsync("msg-3", TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		await _store.MarkProcessedAsync("msg-1", TestHandlerType, CancellationToken.None);
		await _store.MarkFailedAsync("msg-2", TestHandlerType, "error", CancellationToken.None);

		// Assert
		var statistics = await _store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		_ = statistics.ShouldNotBeNull();
		statistics.TotalEntries.ShouldBe(3);
		statistics.ProcessedEntries.ShouldBe(1);
		statistics.FailedEntries.ShouldBe(1);
		statistics.PendingEntries.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateTraceContextThroughOperations()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

		using var parentActivity = new Activity("parent-operation");
		_ = parentActivity.SetParentId(parentContext.TraceId, parentContext.SpanId, parentContext.TraceFlags);
		_ = parentActivity.Start();

		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act
		var entry = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);

		// Verify the trace context was active during the operation
		Activity.Current?.TraceId.ShouldBe(traceId);
	}

	[Fact]
	public async Task RejectDuplicateEntries()
	{
		// Arrange
		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		_ = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		exception.Message.ShouldContain(messageId);
		exception.Message.ShouldContain(TestHandlerType);
	}

	[Fact]
	public async Task ReturnEmptyStatisticsWhenNoEntries()
	{
		// Arrange & Act
		var statistics = await _store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = statistics.ShouldNotBeNull();
		statistics.TotalEntries.ShouldBe(0);
		statistics.ProcessedEntries.ShouldBe(0);
		statistics.FailedEntries.ShouldBe(0);
		statistics.PendingEntries.ShouldBe(0);
	}

	[Fact]
	public async Task ReportIsProcessedCorrectly()
	{
		// Arrange
		var messageId = "test-message";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		_ = await _store.CreateEntryAsync(messageId, TestHandlerType, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert - not processed initially
		var isProcessedBefore = await _store.IsProcessedAsync(messageId, TestHandlerType, CancellationToken.None).ConfigureAwait(false);
		isProcessedBefore.ShouldBeFalse();

		// Act - mark processed
		await _store.MarkProcessedAsync(messageId, TestHandlerType, CancellationToken.None);

		// Assert - now processed
		var isProcessedAfter = await _store.IsProcessedAsync(messageId, TestHandlerType, CancellationToken.None).ConfigureAwait(false);
		isProcessedAfter.ShouldBeTrue();
	}

	public void Dispose()
	{
		_otelFixture?.Dispose();
		_store?.Dispose();
	}
}
