// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Testing.Conformance;
using InMemoryInboxOptions = Excalibur.Data.InMemory.Inbox.InMemoryInboxOptions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryInboxStore"/> validating IInboxStore contract compliance.
/// </summary>
/// <remarks>
/// InMemoryInboxStore directly implements <see cref="IInboxStore"/> from Excalibur.Dispatch.Abstractions.Inbox,
/// so no adapter is needed. The conformance test kit validates the contract compliance.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public class InMemoryInboxStoreConformanceTests : InboxStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IInboxStore CreateStore()
	{
		var options = Options.Create(new InMemoryInboxOptions
		{
			MaxEntries = 10000,
			RetentionPeriod = TimeSpan.FromHours(24)
		});

		var logger = NullLogger<InMemoryInboxStore>.Instance;
		return new InMemoryInboxStore(options, logger);
	}

	#region Create Tests

	[Fact]
	public Task CreateEntryAsync_NewEntry_ShouldSucceed_Test() =>
		CreateEntryAsync_NewEntry_ShouldSucceed();

	[Fact]
	public Task CreateEntryAsync_DuplicateEntry_ShouldThrow_Test() =>
		CreateEntryAsync_DuplicateEntry_ShouldThrow();

	[Fact]
	public Task CreateEntryAsync_WithAllMetadata_ShouldPreserve_Test() =>
		CreateEntryAsync_WithAllMetadata_ShouldPreserve();

	#endregion Create Tests

	#region Process Tests

	[Fact]
	public Task MarkProcessedAsync_ExistingEntry_ShouldSucceed_Test() =>
		MarkProcessedAsync_ExistingEntry_ShouldSucceed();

	[Fact]
	public Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue_Test() =>
		TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();

	[Fact]
	public Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse_Test() =>
		TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse();

	[Fact]
	public Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue_Test() =>
		IsProcessedAsync_ProcessedMessage_ShouldReturnTrue();

	[Fact]
	public Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse_Test() =>
		IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse();

	#endregion Process Tests

	#region Fail Tests

	[Fact]
	public Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError_Test() =>
		MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError();

	[Fact]
	public Task MarkFailedAsync_ShouldIncrementRetryCount_Test() =>
		MarkFailedAsync_ShouldIncrementRetryCount();

	[Fact]
	public Task GetFailedEntriesAsync_ShouldRespectMaxRetries_Test() =>
		GetFailedEntriesAsync_ShouldRespectMaxRetries();

	#endregion Fail Tests

	#region Query Tests

	[Fact]
	public Task GetEntryAsync_Existing_ShouldReturnEntry_Test() =>
		GetEntryAsync_Existing_ShouldReturnEntry();

	[Fact]
	public Task GetEntryAsync_NonExistent_ShouldReturnNull_Test() =>
		GetEntryAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetStatisticsAsync_ShouldReturnCorrectCounts_Test() =>
		GetStatisticsAsync_ShouldReturnCorrectCounts();

	#endregion Query Tests

	#region Cleanup Tests

	[Fact]
	public Task CleanupAsync_OldProcessed_ShouldRemove_Test() =>
		CleanupAsync_OldProcessed_ShouldRemove();

	[Fact]
	public Task CleanupAsync_ShouldPreserveRecent_Test() =>
		CleanupAsync_ShouldPreserveRecent();

	#endregion Cleanup Tests

	#region Isolation Tests

	[Fact]
	public Task Entries_ShouldIsolateByMessageIdAndHandlerType_Test() =>
		Entries_ShouldIsolateByMessageIdAndHandlerType();

	[Fact]
	public Task SameMessageId_DifferentHandlers_ShouldBeIndependent_Test() =>
		SameMessageId_DifferentHandlers_ShouldBeIndependent();

	#endregion Isolation Tests

	#region Edge Cases

	[Fact]
	public Task GetAllEntriesAsync_ShouldReturnAllEntries_Test() =>
		GetAllEntriesAsync_ShouldReturnAllEntries();

	#endregion Edge Cases
}
