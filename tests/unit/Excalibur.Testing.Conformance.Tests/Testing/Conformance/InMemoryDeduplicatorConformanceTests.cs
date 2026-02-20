// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryDeduplicator"/> validating IInMemoryDeduplicator contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryDeduplicator provides lightweight message deduplication for light-mode inbox processing
/// where persistent storage is not required. Uses thread-safe ConcurrentDictionary storage.
/// </para>
/// <para>
/// <strong>MESSAGING INFRASTRUCTURE:</strong> IInMemoryDeduplicator implements the duplicate
/// detection pattern for message processing idempotency.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>IsDuplicateAsync throws ArgumentException on null/empty/whitespace messageId</description></item>
/// <item><description>IsDuplicateAsync throws ArgumentOutOfRangeException on zero expiry</description></item>
/// <item><description>IsDuplicateAsync returns false for unprocessed messages</description></item>
/// <item><description>MarkProcessedAsync marks messages for duplicate detection</description></item>
/// <item><description>DUPLICATE-CHECK pattern: Mark → IsDuplicate returns true</description></item>
/// <item><description>Expiry: Expired entries no longer detected as duplicates</description></item>
/// <item><description>CleanupExpiredEntriesAsync removes expired entries and returns count</description></item>
/// <item><description>GetStatistics (SYNC!) returns valid statistics</description></item>
/// <item><description>ClearAsync removes all tracked messages</description></item>
/// <item><description>IDisposable: Proper cleanup timer disposal</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "DEDUPLICATOR")]
public class InMemoryDeduplicatorConformanceTests : DeduplicatorConformanceTestKit
{
	/// <inheritdoc />
	protected override IInMemoryDeduplicator CreateDeduplicator()
	{
		var logger = NullLogger<InMemoryDeduplicator>.Instance;
		return new InMemoryDeduplicator(logger);
	}

	#region IsDuplicateAsync Tests

	[Fact]
	public Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException_Test() =>
		IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException_Test() =>
		IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException_Test() =>
		IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() =>
		IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();

	[Fact]
	public Task IsDuplicateAsync_NotProcessed_ShouldReturnFalse_Test() =>
		IsDuplicateAsync_NotProcessed_ShouldReturnFalse();

	#endregion IsDuplicateAsync Tests

	#region MarkProcessedAsync Tests

	[Fact]
	public Task MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException_Test() =>
		MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() =>
		MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();

	[Fact]
	public Task MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue_Test() =>
		MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue();

	#endregion MarkProcessedAsync Tests

	#region Expiry Tests

	[Fact]
	public Task IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse_Test() =>
		IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse();

	[Fact]
	public Task CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount_Test() =>
		CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount();

	#endregion Expiry Tests

	#region GetStatistics Tests (SYNC!)

	[Fact]
	public Task GetStatistics_ShouldReturnValidStatistics_Test() =>
		GetStatistics_ShouldReturnValidStatistics();

	[Fact]
	public Task GetStatistics_AfterChecks_ShouldIncrementTotalChecks_Test() =>
		GetStatistics_AfterChecks_ShouldIncrementTotalChecks();

	[Fact]
	public Task GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected_Test() =>
		GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected();

	#endregion GetStatistics Tests (SYNC!)

	#region ClearAsync Tests

	[Fact]
	public Task ClearAsync_ShouldRemoveAllEntries_Test() =>
		ClearAsync_ShouldRemoveAllEntries();

	[Fact]
	public Task ClearAsync_ShouldResetTrackedMessageCount_Test() =>
		ClearAsync_ShouldResetTrackedMessageCount();

	#endregion ClearAsync Tests

	#region Disposable Tests

	[Fact]
	public Task DisposedDeduplicator_ShouldThrowObjectDisposedException_Test() =>
		DisposedDeduplicator_ShouldThrowObjectDisposedException();

	#endregion Disposable Tests
}
