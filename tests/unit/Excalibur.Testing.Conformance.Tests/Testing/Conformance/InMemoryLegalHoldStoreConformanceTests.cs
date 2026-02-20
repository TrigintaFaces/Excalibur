// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryLegalHoldStore"/> validating ILegalHoldStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryLegalHoldStore uses an instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> ILegalHoldStore implements GDPR Article 17(3) "Legal Hold Exceptions".
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SaveHoldAsync THROWS InvalidOperationException on duplicate HoldId</description></item>
/// <item><description>SaveHoldAsync and UpdateHoldAsync THROW ArgumentNullException on null hold</description></item>
/// <item><description>GetActiveHoldsForDataSubjectAsync THROWS ArgumentException on null/whitespace</description></item>
/// <item><description>GetActiveHoldsForTenantAsync THROWS ArgumentException on null/whitespace tenantId</description></item>
/// <item><description>Active vs Expired vs Released state distinctions enforced</description></item>
/// <item><description>GetExpiredHoldsAsync returns only active holds with passed ExpiresAt</description></item>
/// <item><description>ListActiveHoldsAsync orders by CreatedAt descending (newest first)</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public class InMemoryLegalHoldStoreConformanceTests : LegalHoldStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override ILegalHoldStore CreateStore() => new InMemoryLegalHoldStore();

	#region Save Lifecycle Tests

	[Fact]
	public Task SaveHoldAsync_ShouldPersistHold_Test() =>
		SaveHoldAsync_ShouldPersistHold();

	[Fact]
	public Task SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException_Test() =>
		SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task SaveHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() =>
		SaveHoldAsync_NullHold_ShouldThrowArgumentNullException();

	#endregion Save Lifecycle Tests

	#region Retrieval Tests

	[Fact]
	public Task GetHoldAsync_ExistingHold_ShouldReturnHold_Test() =>
		GetHoldAsync_ExistingHold_ShouldReturnHold();

	[Fact]
	public Task GetHoldAsync_NonExistent_ShouldReturnNull_Test() =>
		GetHoldAsync_NonExistent_ShouldReturnNull();

	#endregion Retrieval Tests

	#region Update Tests

	[Fact]
	public Task UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue_Test() =>
		UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue();

	[Fact]
	public Task UpdateHoldAsync_NonExistent_ShouldReturnFalse_Test() =>
		UpdateHoldAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() =>
		UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException();

	#endregion Update Tests

	#region Data Subject Holds Tests

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching_Test() =>
		GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching();

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException_Test() =>
		GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException();

	#endregion Data Subject Holds Tests

	#region Tenant Holds Tests

	[Fact]
	public Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching_Test() =>
		GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching();

	[Fact]
	public Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException_Test() =>
		GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException();

	#endregion Tenant Holds Tests

	#region List Active Tests

	[Fact]
	public Task ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc_Test() =>
		ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc();

	[Fact]
	public Task ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly();

	#endregion List Active Tests

	#region List All Tests

	[Fact]
	public Task ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll_Test() =>
		ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll();

	[Fact]
	public Task ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly_Test() =>
		ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly();

	#endregion List All Tests

	#region Expired Holds Tests

	[Fact]
	public Task GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration_Test() =>
		GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration();

	[Fact]
	public Task GetExpiredHoldsAsync_ShouldExcludeReleasedHolds_Test() =>
		GetExpiredHoldsAsync_ShouldExcludeReleasedHolds();

	#endregion Expired Holds Tests
}
