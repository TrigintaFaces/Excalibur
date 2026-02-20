// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Orchestration;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemorySagaStore"/> validating ISagaStore contract compliance.
/// </summary>
/// <remarks>
/// InMemorySagaStore directly implements <see cref="ISagaStore"/> from Excalibur.Dispatch.Abstractions.Messaging.Delivery,
/// so no adapter is needed. The conformance test kit validates the contract compliance.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public class InMemorySagaStoreConformanceTests : SagaStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override ISagaStore CreateStore() => new InMemorySagaStore();

	#region Save Tests

	[Fact]
	public Task SaveAsync_NewSaga_ShouldSucceed_Test() =>
		SaveAsync_NewSaga_ShouldSucceed();

	[Fact]
	public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() =>
		SaveAsync_ExistingSaga_ShouldUpdate();

	[Fact]
	public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() =>
		SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();

	#endregion Save Tests

	#region Load Tests

	[Fact]
	public Task LoadAsync_NonExistent_ShouldReturnNull_Test() =>
		LoadAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() =>
		LoadAsync_ExistingSaga_ShouldReturnState();

	[Fact]
	public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() =>
		LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();

	#endregion Load Tests

	#region Round-Trip Tests

	[Fact]
	public Task SaveAndLoad_ShouldPreserveAllProperties_Test() =>
		SaveAndLoad_ShouldPreserveAllProperties();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() =>
		SaveAndLoad_ShouldPreserveDateTimeValues();

	#endregion Round-Trip Tests

	#region Isolation Tests

	[Fact]
	public Task Sagas_ShouldIsolateBySagaId_Test() =>
		Sagas_ShouldIsolateBySagaId();

	[Fact]
	public Task UpdateOneSaga_ShouldNotAffectOthers_Test() =>
		UpdateOneSaga_ShouldNotAffectOthers();

	#endregion Isolation Tests

	#region Edge Cases

	[Fact]
	public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() =>
		SaveAsync_WithDefaultValues_ShouldSucceed();

	#endregion Edge Cases
}
