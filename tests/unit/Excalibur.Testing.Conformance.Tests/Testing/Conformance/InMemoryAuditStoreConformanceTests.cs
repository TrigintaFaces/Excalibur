// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;
using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryAuditStore"/> validating IAuditStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryAuditStore uses an instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>StoreAsync THROWS InvalidOperationException on duplicate EventId</description></item>
/// <item><description>Hash chain integrity via PreviousEventHash and EventHash</description></item>
/// <item><description>Multi-tenant isolation with "_default_" for null TenantId</description></item>
/// <item><description>Genesis hash for first event in tenant chain</description></item>
/// <item><description>QueryAsync supports 11 filter criteria + pagination + ordering</description></item>
/// <item><description>VerifyChainIntegrityAsync detects tampering (COMPLIANCE-CRITICAL)</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public class InMemoryAuditStoreConformanceTests : AuditStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IAuditStore CreateStore() => new InMemoryAuditStore();

	#region Store Tests

	[Fact]
	public Task StoreAsync_ShouldPersistEvent_Test() =>
		StoreAsync_ShouldPersistEvent();

	[Fact]
	public Task StoreAsync_WithNullEvent_ShouldThrow_Test() =>
		StoreAsync_WithNullEvent_ShouldThrow();

	[Fact]
	public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

	#endregion Store Tests

	#region Retrieval Tests

	[Fact]
	public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() =>
		GetByIdAsync_ExistingEvent_ShouldReturnEvent();

	[Fact]
	public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() =>
		GetByIdAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() =>
		GetByIdAsync_NullOrEmpty_ShouldThrow();

	#endregion Retrieval Tests

	#region Query Tests

	[Fact]
	public Task QueryAsync_ByDateRange_ShouldReturnMatching_Test() =>
		QueryAsync_ByDateRange_ShouldReturnMatching();

	[Fact]
	public Task QueryAsync_ByEventType_ShouldFilter_Test() =>
		QueryAsync_ByEventType_ShouldFilter();

	[Fact]
	public Task QueryAsync_ByActorId_ShouldFilter_Test() =>
		QueryAsync_ByActorId_ShouldFilter();

	[Fact]
	public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() =>
		QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

	#endregion Query Tests

	#region Count Tests

	[Fact]
	public Task CountAsync_WithFilters_ShouldReturnCount_Test() =>
		CountAsync_WithFilters_ShouldReturnCount();

	[Fact]
	public Task CountAsync_EmptyResult_ShouldReturnZero_Test() =>
		CountAsync_EmptyResult_ShouldReturnZero();

	#endregion Count Tests

	#region Integrity Tests

	[Fact]
	public Task VerifyChainIntegrityAsync_ValidChain_ShouldReturnValid_Test() =>
		VerifyChainIntegrityAsync_ValidChain_ShouldReturnValid();

	[Fact]
	public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReturnValidWithZeroEvents_Test() =>
		VerifyChainIntegrityAsync_EmptyRange_ShouldReturnValidWithZeroEvents();

	#endregion Integrity Tests

	#region LastEvent Tests

	[Fact]
	public Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant_Test() =>
		GetLastEventAsync_WithTenant_ShouldReturnLastForTenant();

	[Fact]
	public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() =>
		GetLastEventAsync_DefaultTenant_ShouldReturnLast();

	#endregion LastEvent Tests

	#region Hash Chain Tests

	[Fact]
	public Task StoreAsync_ShouldSetPreviousEventHash_Test() =>
		StoreAsync_ShouldSetPreviousEventHash();

	[Fact]
	public Task StoreAsync_ShouldComputeEventHash_Test() =>
		StoreAsync_ShouldComputeEventHash();

	#endregion Hash Chain Tests
}
