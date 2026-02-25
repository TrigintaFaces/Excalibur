// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryDataInventoryStore"/> validating IDataInventoryStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryDataInventoryStore uses instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IDataInventoryStore implements GDPR RoPA (Records of Processing Activities) - Article 30.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>SaveRegistrationAsync UPSERTS by TableName:FieldName key (does NOT throw on duplicate)</description></item>
/// <item><description>SaveRegistrationAsync THROWS ArgumentNullException on null registration</description></item>
/// <item><description>RecordDiscoveredLocationAsync THROWS ArgumentNullException on null location</description></item>
/// <item><description>RecordDiscoveredLocationAsync THROWS ArgumentException on null/whitespace dataSubjectId</description></item>
/// <item><description>RecordDiscoveredLocationAsync deduplicates by TableName+FieldName+RecordId</description></item>
/// <item><description>GetDataMapEntriesAsync merges registrations with discovered locations</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "STORE")]
public class InMemoryDataInventoryStoreConformanceTests : DataInventoryStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override IDataInventoryStore CreateStore() => new InMemoryDataInventoryStore();

	#region Registration Save Tests

	[Fact]
	public Task SaveRegistrationAsync_ShouldPersistRegistration_Test() =>
		SaveRegistrationAsync_ShouldPersistRegistration();

	[Fact]
	public Task SaveRegistrationAsync_DuplicateKey_ShouldUpsert_Test() =>
		SaveRegistrationAsync_DuplicateKey_ShouldUpsert();

	[Fact]
	public Task SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException_Test() =>
		SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException();

	#endregion Registration Save Tests

	#region Registration Remove Tests

	[Fact]
	public Task RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue_Test() =>
		RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue();

	[Fact]
	public Task RemoveRegistrationAsync_NonExistent_ShouldReturnFalse_Test() =>
		RemoveRegistrationAsync_NonExistent_ShouldReturnFalse();

	#endregion Registration Remove Tests

	#region Registration Query Tests

	[Fact]
	public Task GetAllRegistrationsAsync_ShouldReturnAllRegistrations_Test() =>
		GetAllRegistrationsAsync_ShouldReturnAllRegistrations();

	[Fact]
	public Task FindRegistrationsForDataSubjectAsync_ShouldFilterByIdTypeAndTenant_Test() =>
		FindRegistrationsForDataSubjectAsync_ShouldFilterByIdTypeAndTenant();

	#endregion Registration Query Tests

	#region Discovered Locations Save Tests

	[Fact]
	public Task RecordDiscoveredLocationAsync_ShouldPersistLocation_Test() =>
		RecordDiscoveredLocationAsync_ShouldPersistLocation();

	[Fact]
	public Task RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException_Test() =>
		RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException();

	[Fact]
	public Task RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException_Test() =>
		RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException();

	[Fact]
	public Task RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate_Test() =>
		RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate();

	#endregion Discovered Locations Save Tests

	#region Discovered Locations Query Tests

	[Fact]
	public Task GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations_Test() =>
		GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations();

	[Fact]
	public Task GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList_Test() =>
		GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList();

	#endregion Discovered Locations Query Tests

	#region Data Map Tests

	[Fact]
	public Task GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered_Test() =>
		GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered();

	[Fact]
	public Task GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse_Test() =>
		GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse();

	[Fact]
	public Task GetDataMapEntriesAsync_ShouldCalculateRecordCount_Test() =>
		GetDataMapEntriesAsync_ShouldCalculateRecordCount();

	#endregion Data Map Tests

	#region Multi-Tenant Tests

	[Fact]
	public Task FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries_Test() =>
		GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries();

	[Fact]
	public Task GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject_Test() =>
		GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject();

	#endregion Multi-Tenant Tests
}
