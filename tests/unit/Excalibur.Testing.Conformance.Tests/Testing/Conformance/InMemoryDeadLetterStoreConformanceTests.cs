// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryDeadLetterStore"/> validating IDeadLetterStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryDeadLetterStore uses an instance-level ConcurrentDictionary keyed by Id (internal key),
/// but all API methods (GetByIdAsync, DeleteAsync, MarkAsReplayedAsync) search by MessageId.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>Store/retrieval operations use MessageId for API lookups</description></item>
/// <item><description>MarkAsReplayedAsync sets IsReplayed=true and ReplayedAt timestamp</description></item>
/// <item><description>DeleteAsync physically removes messages (unlike ScheduleStore soft-delete)</description></item>
/// <item><description>CleanupOldMessagesAsync removes messages older than retention period</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryDeadLetterStoreConformanceTests : DeadLetterStoreConformanceTestKit
{
	/// <inheritdoc />
	/// <remarks>
	/// The in-memory store scopes by ambient tenant context, so binding it to a partition means supplying
	/// a context that reports that tenant. Each call builds a fresh store, which is what makes the
	/// isolation arms meaningful: two partitions produce two independent stores, and an implementation
	/// that ignored the partition would still pass the safety arm while failing the liveness arms.
	/// </remarks>
	/// <remarks>
	/// The context is passed straight through. The store resolves it on every operation, so the kit can
	/// switch the ambient tenant on one store instance — which is what makes the isolation arm capable of
	/// failing. <see langword="null"/> is a single-tenant host, which the store folds onto the reserved
	/// untenanted partition itself.
	/// </remarks>
	protected override IDeadLetterStore CreateStore(ITenantContext? ambientTenant) =>
		new InMemoryDeadLetterStore(ambientTenant, NullLogger<InMemoryDeadLetterStore>.Instance);

	#region Store Tests

	[Fact]
	public Task StoreAsync_ShouldPersistMessage_Test() =>
		StoreAsync_ShouldPersistMessage();

	[Fact]
	public Task StoreAsync_WithNullMessage_ShouldThrow_Test() =>
		StoreAsync_WithNullMessage_ShouldThrow();

	[Fact]
	public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() =>
		StoreAsync_MultipleMessages_ShouldPersistAll();

	#endregion Store Tests

	#region Retrieval Tests

	[Fact]
	public Task GetMessagesAsync_EmptyStore_ShouldReturnEmpty_Test() =>
		GetMessagesAsync_EmptyStore_ShouldReturnEmpty();

	[Fact]
	public Task GetByIdAsync_ShouldReturnMessageByMessageId_Test() =>
		GetByIdAsync_ShouldReturnMessageByMessageId();

	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() =>
		GetByIdAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetMessagesAsync_FilterByMessageType_ShouldFilter_Test() =>
		GetMessagesAsync_FilterByMessageType_ShouldFilter();

	[Fact]
	public Task GetMessagesAsync_Pagination_ShouldRespectMaxResults_Test() =>
		GetMessagesAsync_Pagination_ShouldRespectMaxResults();

	#endregion Retrieval Tests

	#region Replay Tests

	[Fact]
	public Task MarkAsReplayedAsync_ShouldSetIsReplayedTrue_Test() =>
		MarkAsReplayedAsync_ShouldSetIsReplayedTrue();

	[Fact]
	public Task MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent_Test() =>
		MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent();

	[Fact]
	public Task MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent_Test() =>
		MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent();

	#endregion Replay Tests

	#region Delete Tests

	[Fact]
	public Task DeleteAsync_ShouldRemoveAndReturnTrue_Test() =>
		DeleteAsync_ShouldRemoveAndReturnTrue();

	[Fact]
	public Task DeleteAsync_NonExistent_ShouldReturnFalse_Test() =>
		DeleteAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task DeleteAsync_ShouldDecreaseCount_Test() =>
		DeleteAsync_ShouldDecreaseCount();

	#endregion Delete Tests

	#region Count Tests

	[Fact]
	public Task GetCountAsync_EmptyStore_ShouldReturnZero_Test() =>
		GetCountAsync_EmptyStore_ShouldReturnZero();

	[Fact]
	public Task GetCountAsync_AfterStores_ShouldReturnCorrectCount_Test() =>
		GetCountAsync_AfterStores_ShouldReturnCorrectCount();

	#endregion Count Tests

	#region Cleanup Tests

	[Fact]
	public Task CleanupOldMessagesAsync_ShouldRemoveOldMessages_Test() =>
		CleanupOldMessagesAsync_ShouldRemoveOldMessages();

	[Fact]
	public Task CleanupOldMessagesAsync_ShouldRespectRetention_Test() =>
		CleanupOldMessagesAsync_ShouldRespectRetention();

	#endregion Cleanup Tests

	#region Tenant Isolation Tests

	[Fact]
	public Task TenantScopedRead_MustNotSeeAnotherTenantsEntry_Test() =>
		TenantScopedRead_MustNotSeeAnotherTenantsEntry();

	[Fact]
	public Task TenantScopedRead_MustSeeItsOwnEntry_Test() =>
		TenantScopedRead_MustSeeItsOwnEntry();

	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEntry_Test() =>
		UntenantedPartition_MustRoundTripItsOwnEntry();

	#endregion Tenant Isolation Tests
}
