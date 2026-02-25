// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Outbox;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryOutboxStore"/> validating IOutboxStore contract compliance.
/// </summary>
/// <remarks>
/// InMemoryOutboxStore directly implements <see cref="IOutboxStore"/> from Excalibur.Dispatch.Abstractions.Outbox.
/// A minimal NullUtf8JsonSerializer is used since conformance tests use StageMessageAsync (not EnqueueAsync).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public class InMemoryOutboxStoreConformanceTests : OutboxStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IOutboxStore CreateStore()
	{
		var options = Options.Create(new InMemoryOutboxOptions());
		var logger = NullLogger<InMemoryOutboxStore>.Instance;
		return new InMemoryOutboxStore(options, logger);
	}

	#region Stage Tests

	[Fact]
	public Task StageMessageAsync_NewMessage_ShouldSucceed_Test() =>
		StageMessageAsync_NewMessage_ShouldSucceed();

	[Fact]
	public Task StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly_Test() =>
		StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly();

	#endregion Stage Tests

	#region Retrieval Tests

	[Fact]
	public Task GetUnsentMessagesAsync_ShouldReturnStagedMessages_Test() =>
		GetUnsentMessagesAsync_ShouldReturnStagedMessages();

	[Fact]
	public Task GetUnsentMessagesAsync_ShouldRespectBatchSize_Test() =>
		GetUnsentMessagesAsync_ShouldRespectBatchSize();

	#endregion Retrieval Tests

	#region Sent Tests

	[Fact]
	public Task MarkSentAsync_ExistingMessage_ShouldSetSentAt_Test() =>
		MarkSentAsync_ExistingMessage_ShouldSetSentAt();

	[Fact]
	public Task MarkSentAsync_ShouldExcludeFromUnsent_Test() =>
		MarkSentAsync_ShouldExcludeFromUnsent();

	[Fact]
	public Task MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException_Test() =>
		MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException();

	#endregion Sent Tests

	#region Failure Tests

	[Fact]
	public Task MarkFailedAsync_ShouldSetErrorMessage_Test() =>
		MarkFailedAsync_ShouldSetErrorMessage();

	[Fact]
	public Task MarkFailedAsync_ShouldSetRetryCount_Test() =>
		MarkFailedAsync_ShouldSetRetryCount();

	[Fact]
	public Task GetFailedMessagesAsync_ShouldRespectMaxRetries_Test() =>
		GetFailedMessagesAsync_ShouldRespectMaxRetries();

	[Fact]
	public Task GetFailedMessagesAsync_ShouldRespectOlderThan_Test() =>
		GetFailedMessagesAsync_ShouldRespectOlderThan();

	#endregion Failure Tests

	#region Scheduled Tests

	[Fact]
	public Task GetScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold_Test() =>
		GetScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold();

	[Fact]
	public Task GetScheduledMessagesAsync_ShouldNotReturnImmediateMessages_Test() =>
		GetScheduledMessagesAsync_ShouldNotReturnImmediateMessages();

	#endregion Scheduled Tests

	#region Cleanup Tests

	[Fact]
	public Task CleanupSentMessagesAsync_ShouldRemoveOldMessages_Test() =>
		CleanupSentMessagesAsync_ShouldRemoveOldMessages();

	[Fact]
	public Task CleanupSentMessagesAsync_ShouldRespectBatchSize_Test() =>
		CleanupSentMessagesAsync_ShouldRespectBatchSize();

	#endregion Cleanup Tests

	#region Statistics Tests

	[Fact]
	public Task GetStatisticsAsync_ShouldReflectMessageCounts_Test() =>
		GetStatisticsAsync_ShouldReflectMessageCounts();

	[Fact]
	public Task GetStatisticsAsync_AfterOperations_ShouldUpdateAccurately_Test() =>
		GetStatisticsAsync_AfterOperations_ShouldUpdateAccurately();

	#endregion Statistics Tests
}
