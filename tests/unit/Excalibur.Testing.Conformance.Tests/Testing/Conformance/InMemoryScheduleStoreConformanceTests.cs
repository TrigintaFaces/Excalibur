// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryScheduleStore"/> validating IScheduleStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryScheduleStore uses an instance-level ConcurrentDictionary, so no special
/// static state isolation is required (unlike InMemoryLeaderElection).
/// </para>
/// <para>
/// Key behavior verified: CompleteAsync sets Enabled=false but does NOT remove the message from the store.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public class InMemoryScheduleStoreConformanceTests : ScheduleStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IScheduleStore CreateStore() =>
		new InMemoryScheduleStore();

	#region Store Tests

	[Fact]
	public Task StoreAsync_ShouldPersistMessage_Test() =>
		StoreAsync_ShouldPersistMessage();

	[Fact]
	public Task StoreAsync_WithNullMessage_ShouldThrow_Test() =>
		StoreAsync_WithNullMessage_ShouldThrow();

	[Fact]
	public Task StoreAsync_SameId_ShouldUpsert_Test() =>
		StoreAsync_SameId_ShouldUpsert();

	[Fact]
	public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() =>
		StoreAsync_MultipleMessages_ShouldPersistAll();

	#endregion Store Tests

	#region Retrieval Tests

	[Fact]
	public Task GetAllAsync_EmptyStore_ShouldReturnEmpty_Test() =>
		GetAllAsync_EmptyStore_ShouldReturnEmpty();

	[Fact]
	public Task GetAllAsync_AfterStore_ShouldReturnMessage_Test() =>
		GetAllAsync_AfterStore_ShouldReturnMessage();

	[Fact]
	public Task GetAllAsync_ShouldReturnAllMessages_Test() =>
		GetAllAsync_ShouldReturnAllMessages();

	#endregion Retrieval Tests

	#region Completion Tests

	[Fact]
	public Task CompleteAsync_ShouldSetEnabledFalse_Test() =>
		CompleteAsync_ShouldSetEnabledFalse();

	[Fact]
	public Task CompleteAsync_NonExistent_ShouldBeIdempotent_Test() =>
		CompleteAsync_NonExistent_ShouldBeIdempotent();

	[Fact]
	public Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent_Test() =>
		CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();

	#endregion Completion Tests

	#region Integration Tests

	[Fact]
	public Task StoreAsync_ThenComplete_MessageRemainsPersisted_Test() =>
		StoreAsync_ThenComplete_MessageRemainsPersisted();

	[Fact]
	public Task MultipleMessages_CompleteOne_OthersUnaffected_Test() =>
		MultipleMessages_CompleteOne_OthersUnaffected();

	#endregion Integration Tests
}
