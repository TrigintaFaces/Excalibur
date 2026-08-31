using Excalibur.Compliance.Erasure;
using Microsoft.Extensions.Options;
// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="InMemoryDataInventoryStore"/>.
/// Tests data registration, discovery, and data map functionality per ADR-054.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class InMemoryDataInventoryStoreShould
{
	private readonly InMemoryDataInventoryStore _sut;

	public InMemoryDataInventoryStoreShould()
	{
		_sut = new InMemoryDataInventoryStore(new MovableTenantContext(), Microsoft.Extensions.Options.Options.Create(new TenantContextOptions()));
	}

	#region SaveRegistrationAsync Tests

	[Fact]
	public async Task SaveRegistrationAsync_ThrowsArgumentNullException_WhenRegistrationIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveRegistrationAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SaveRegistrationAsync_StoresRegistration()
	{
		// Arrange
		var registration = CreateRegistration();

		// Act
		await _sut.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.RegistrationCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveRegistrationAsync_OverwritesExistingRegistration()
	{
		// Arrange
		var registration1 = CreateRegistration("Users", "Email");
		var registration2 = CreateRegistration("Users", "Email") with { Description = "Updated" };

		// Act
		await _sut.SaveRegistrationAsync(registration1, CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(registration2, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.RegistrationCount.ShouldBe(1);
	}

	[Fact]
	public async Task SaveRegistrationAsync_StoresMultipleDifferentRegistrations()
	{
		// Arrange & Act
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Email"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Phone"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Orders", "CustomerEmail"), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.RegistrationCount.ShouldBe(3);
	}

	#endregion

	#region RemoveRegistrationAsync Tests

	[Fact]
	public async Task RemoveRegistrationAsync_ReturnsFalse_WhenRegistrationDoesNotExist()
	{
		// Act
		var result = await _sut.RemoveRegistrationAsync(
			"NonExistent", "Field", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RemoveRegistrationAsync_ReturnsTrue_WhenRegistrationRemoved()
	{
		// Arrange
		var registration = CreateRegistration();
		await _sut.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RemoveRegistrationAsync(
			registration.TableName, registration.FieldName, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_sut.RegistrationCount.ShouldBe(0);
	}

	[Fact]
	public async Task RemoveRegistrationAsync_DoesNotAffectOtherRegistrations()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Email"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Phone"), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.RemoveRegistrationAsync("Users", "Email", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		_sut.RegistrationCount.ShouldBe(1);
	}

	#endregion

	#region GetAllRegistrationsAsync Tests

	[Fact]
	public async Task GetAllRegistrationsAsync_ReturnsEmptyList_WhenNoRegistrations()
	{
		// Act
		var result = await _sut.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetAllRegistrationsAsync_ReturnsAllRegistrations()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Email"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Orders", "CustomerName"), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
	}

	#endregion

	#region FindRegistrationsForDataSubjectAsync Tests

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_ReturnsEmptyList_WhenNoMatchingRegistrations()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(
			CreateRegistration() with { IdType = DataSubjectIdType.Email },
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.FindRegistrationsForDataSubjectAsync(
			"subject-1",
			DataSubjectIdType.ExternalId,
			null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_FiltersByIdType()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Users", "Email") with { IdType = DataSubjectIdType.Email },
			CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Customers", "Id") with { IdType = DataSubjectIdType.ExternalId },
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].IdType.ShouldBe(DataSubjectIdType.Email);
	}

	// TENANT ISOLATION -- the arms below replace two tests that certified the defect as correct
	// behaviour. Both prior fixtures assigned a tenant VALUE ("tenant-1") to TenantIdColumn, which holds a
	// column NAME ("TenantId"). The store compared that field to the caller's tenant value, so the fixture
	// and the implementation shared one category error and agreed with each other: "tenant-1" == "tenant-1"
	// passed, and the suite reported a working filter over a predicate that cannot filter. A fixture that
	// encodes the implementation's misconception is not a weak lock, it is an inverted one -- it fails when
	// the code is corrected. TenantIdColumn now carries a column name in every fixture, and the tenant value
	// travels in TenantId, matching the shape the SQL providers already store and read.

	// The scope is AMBIENT, so the fixture has to move it. The store resolves its tenant per call from
	// ITenantContext and deliberately ignores the tenantId argument on the read: a caller must not be able
	// to widen a read by omitting a tenant, nor redirect one by naming another tenant. A fixture that
	// passed the argument instead would drive a parameter the store discards and prove nothing -- so
	// MovableTenantContext is what stands in for two different callers, and every arm below writes under
	// one scope and reads under another.
	//
	// It implements ITenantContext directly, deriving from no first-party base: a fake that inherits its
	// members from production code re-tests that base rather than the contract, and would keep passing for
	// an implementation that reads the ambient tenant wrongly.
	private sealed class MovableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	[Fact]
	public async Task BindTheSameTermRegardlessOfWhichContextIsRegistered_WhenTheHostIsSingleTenant()
	{
		// SAFETY, and the arm is about the STORED TERM, not about isolation. The store used to resolve its
		// partition as "is an ITenantContext registered?", so a single-tenant host filed its rows under the
		// untenanted sentinel or under the default-tenant identity depending on whether some -- possibly
		// unrelated -- registration had supplied a context. Two hosts with identical inventory configuration
		// got different data, and a row written in one state was unreadable in the other.
		//
		// Deployment mode is now read from TenantContextOptions.RequireTenant, so a host that has not opted
		// into multi-tenancy binds one term whatever context is present. The property is asserted the way a
		// caller can observe it: a row written while the ambient context named one tenant is still readable
		// while it names another, because in single-tenant mode neither name reaches the term.
		//
		// NON-VACUITY: against the previous resolution this is RED. There the write bound "tenant-b" and the
		// read bound "tenant-a", so the registration was invisible and ShouldContain fails.
		var tenant = new MovableTenantContext { TenantId = "tenant-b" };
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions()));

		await store.SaveRegistrationAsync(
			CreateRegistration("SingleTenantTable", "Email"),
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";

		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: null,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		result.ShouldContain(
			r => r.TableName == "SingleTenantTable",
			"a single-tenant host bound a different tenant term on the read than it did on the write, so the "
			+ "deployment mode is still being inferred from the ambient context rather than read from "
			+ "TenantContextOptions.RequireTenant -- the row is now unreachable");
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_NotDiscloseAnotherTenantsRegistration()
	{
		// SAFETY. Tenant-b writes under its own scope; tenant-a then reads under its own. The question is
		// the property -- "can tenant-a's read observe tenant-b's row?" -- never the mechanism that answers
		// it, because a lock written from an assumed mechanism is how the previous one went blind.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		tenant.TenantId = "tenant-b";
		await store.SaveRegistrationAsync(
			CreateRegistration("TenantBTable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		await store.SaveRegistrationAsync(
			CreateRegistration("TenantATable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: null,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Identity is asserted on the registration's OWN identity (table name), never on TenantIdColumn --
		// that field holds the NAME of a column, and a fixture keying off it tests a naming coincidence.
		result.ShouldNotContain(
			r => r.TableName == "TenantBTable",
			"a read scoped to tenant-a disclosed tenant-b's registration");
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_StillReturnTheCallersOwnRegistration()
	{
		// LIVENESS. Paired with the arm above on purpose: "discloses nothing" is satisfied completely by a
		// store that returns nothing to anybody, and inaction is the cheapest way to look safe. This arm is
		// the one that fails if a fix over-corrects into a filter that excludes everything.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		tenant.TenantId = "tenant-b";
		await store.SaveRegistrationAsync(
			CreateRegistration("TenantBTable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		await store.SaveRegistrationAsync(
			CreateRegistration("TenantATable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: null,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		result.ShouldContain(
			r => r.TableName == "TenantATable",
			"the scoped read dropped the caller's own registration");
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_IgnoreACallerSuppliedTenantArgument()
	{
		// SAFETY, authorisation. The read takes a tenantId parameter that the store must NOT honour: if it
		// did, any caller could name another tenant and read it, which is the hole the shipped SQL providers
		// close by binding ambient scope and discarding the argument. Naming tenant-b while scoped to
		// tenant-a must return tenant-a's view -- the parameter is inert, not a selector.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		tenant.TenantId = "tenant-b";
		await store.SaveRegistrationAsync(
			CreateRegistration("TenantBTable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: "tenant-b",
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotContain(
			r => r.TableName == "TenantBTable",
			"naming another tenant in the tenantId argument redirected the read into that tenant's data");
	}

	[Fact]
	public async Task SaveRegistrationAsync_NotLetOneTenantOverwriteAnothersRegistration()
	{
		// SAFETY, write side. The read predicate is only half the boundary: if the registration key is
		// (table, field) with no tenant term, two tenants registering the same table and field are one
		// entry and the second save silently destroys the first. A store can scope its reads perfectly and
		// still lose a tenant's data on write, so the disclosure arm above cannot detect this -- nothing
		// throws, and the loss is visible only to the tenant whose row is already gone.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		tenant.TenantId = "tenant-a";
		await store.SaveRegistrationAsync(
			CreateRegistration("Users", "Email") with
			{
				TenantIdColumn = "TenantId",
				Description = "belongs to tenant-a"
			},
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-b";
		await store.SaveRegistrationAsync(
			CreateRegistration("Users", "Email") with
			{
				TenantIdColumn = "TenantId",
				Description = "belongs to tenant-b"
			},
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: null,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		result.ShouldContain(
			r => r.Description == "belongs to tenant-a",
			"tenant-b's save on the same table and field overwrote tenant-a's registration");
	}

	[Fact]
	public async Task GetDiscoveredLocationsAsync_NotReturnAnUntenantedLocationToATenantedScope()
	{
		// SAFETY. A discovered LOCATION is not a registration: it records that a named data subject's data
		// was found somewhere, so it identifies a PERSON and binds strict tenant equality. The untenanted
		// partition is a scope like any other, not a shared one -- a location written with no tenant must
		// not surface inside a tenant's view. This is the arm that separates the two semantics; a fix that
		// widened locations the way registrations are widened would pass every other arm in this file.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		// The reserved sentinel, NOT null. A present context resolving null means "multi-tenancy is active
		// but no tenant was resolved", which fails closed by design; the sentinel is the storage encoding
		// for a row that genuinely belongs to no tenant, and is the only way to reach that partition.
		tenant.TenantId = TenantScope.UntenantedSentinel;
		await store.RecordDiscoveredLocationAsync(
			new DataLocation
			{
				TableName = "UntenantedTable",
				FieldName = "Email",
				DataCategory = "ContactInfo",
				RecordId = "record-1",
				KeyId = "key-1"
			},
			"subject-1",
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		var result = await store.GetDiscoveredLocationsAsync(
			"subject-1",
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotContain(
			l => l.TableName == "UntenantedTable",
			"a discovered location written under the untenanted partition surfaced in a tenant's scope");
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_StillReturnAnUntenantedRegistrationToATenantedScope()
	{
		// LIVENESS, and the arm most likely to be "corrected" into a bug by someone tightening isolation.
		//
		// A registration carries no person identifier -- it describes SCHEMA SHAPE, which table and column
		// hold personal data. An untenanted one therefore describes a location that applies estate-wide, and
		// dropping it from a scoped read does not protect anyone: it silently shortens the subject-access
		// response, so a data subject is told about fewer places their data lives than actually hold it.
		// Under-reporting is the failure that matters here, and no exception is thrown when it happens.
		//
		// This is deliberately NOT the rule for records that identify a PERSON -- erasure requests and legal
		// holds are subject-linked and bind strict tenant equality, where a broader read IS a disclosure.
		// Same predicate shape, opposite correct answer, decided by what the row is about.
		var tenant = new MovableTenantContext();
		var store = new InMemoryDataInventoryStore(tenant, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

		// The reserved sentinel, NOT null. A present context resolving null means "multi-tenancy is active
		// but no tenant was resolved", which fails closed by design; the sentinel is the storage encoding
		// for a row that genuinely belongs to no tenant, and is the only way to reach that partition.
		tenant.TenantId = TenantScope.UntenantedSentinel;
		await store.SaveRegistrationAsync(
			CreateRegistration("EstateWideTable", "Email") with { TenantIdColumn = "TenantId" },
			CancellationToken.None).ConfigureAwait(false);

		tenant.TenantId = "tenant-a";
		var result = await store.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: null,
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		result.ShouldContain(
			r => r.TableName == "EstateWideTable",
			"an untenanted registration was dropped from a tenant-scoped read, silently shortening the "
			+ "subject-access response");
	}

	#endregion

	#region RecordDiscoveredLocationAsync Tests

	[Fact]
	public async Task RecordDiscoveredLocationAsync_ThrowsArgumentNullException_WhenLocationIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.RecordDiscoveredLocationAsync(null!, "subject-1", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_ThrowsArgumentException_WhenDataSubjectIdIsEmpty()
	{
		// Arrange
		var location = CreateDataLocation();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RecordDiscoveredLocationAsync(location, "", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_ThrowsArgumentException_WhenDataSubjectIdIsWhitespace()
	{
		// Arrange
		var location = CreateDataLocation();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RecordDiscoveredLocationAsync(location, "   ", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_StoresLocation()
	{
		// Arrange
		var location = CreateDataLocation();

		// Act
		await _sut.RecordDiscoveredLocationAsync(location, "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.DataSubjectCount.ShouldBe(1);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_DoesNotDuplicateLocations()
	{
		// Arrange
		var location = CreateDataLocation("Users", "Email", "record-1");

		// Act
		await _sut.RecordDiscoveredLocationAsync(location, "subject-1", CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(location, "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		var locations = await _sut.GetDiscoveredLocationsAsync("subject-1", CancellationToken.None).ConfigureAwait(false);
		locations.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_StoresMultipleLocationsForSameSubject()
	{
		// Arrange
		var location1 = CreateDataLocation("Users", "Email", "record-1");
		var location2 = CreateDataLocation("Users", "Phone", "record-1");

		// Act
		await _sut.RecordDiscoveredLocationAsync(location1, "subject-1", CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(location2, "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		var locations = await _sut.GetDiscoveredLocationsAsync("subject-1", CancellationToken.None).ConfigureAwait(false);
		locations.Count.ShouldBe(2);
	}

	[Fact]
	public async Task RecordDiscoveredLocationAsync_StoresLocationsForDifferentSubjects()
	{
		// Arrange
		var location = CreateDataLocation();

		// Act
		await _sut.RecordDiscoveredLocationAsync(location, "subject-1", CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(location, "subject-2", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_sut.DataSubjectCount.ShouldBe(2);
	}

	#endregion

	#region GetDiscoveredLocationsAsync Tests

	[Fact]
	public async Task GetDiscoveredLocationsAsync_ReturnsEmptyList_WhenNoLocations()
	{
		// Act
		var result = await _sut.GetDiscoveredLocationsAsync("subject-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDiscoveredLocationsAsync_ReturnsLocationsForSubject()
	{
		// Arrange
		await _sut.RecordDiscoveredLocationAsync(CreateDataLocation(), "subject-1", CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(CreateDataLocation("Orders", "CustomerId", "order-1"), "subject-2", CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetDiscoveredLocationsAsync("subject-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	#endregion

	#region GetDataMapEntriesAsync Tests

	[Fact]
	public async Task GetDataMapEntriesAsync_ReturnsEmptyList_WhenNoData()
	{
		// Act
		var result = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDataMapEntriesAsync_ReturnsEntriesFromRegistrations()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Users", "Email"),
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].TableName.ShouldBe("Users");
		result[0].FieldName.ShouldBe("Email");
	}

	[Fact]
	public async Task GetDataMapEntriesAsync_IncludesDiscoveredLocations()
	{
		// Arrange
		var location = CreateDataLocation("Users", "Phone", "record-1") with { IsAutoDiscovered = true };
		await _sut.RecordDiscoveredLocationAsync(location, "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
		result[0].IsAutoDiscovered.ShouldBeTrue();
	}

	[Fact]
	public async Task GetDataMapEntriesAsync_GroupsRegistrationsByTableAndField()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Email"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Phone"), CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(CreateRegistration("Orders", "CustomerEmail"), CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(3);
	}

	[Fact]
	public async Task GetDataMapEntriesAsync_DoesNotDuplicateWhenRegistrationMatchesDiscovery()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(CreateRegistration("Users", "Email"), CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(CreateDataLocation("Users", "Email", "record-1"), "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllData()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordDiscoveredLocationAsync(CreateDataLocation(), "subject-1", CancellationToken.None).ConfigureAwait(false);

		// Act
		_sut.Clear();

		// Assert
		_sut.RegistrationCount.ShouldBe(0);
		_sut.DataSubjectCount.ShouldBe(0);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void RegistrationCount_ReturnsZero_WhenEmpty()
	{
		// Assert
		_sut.RegistrationCount.ShouldBe(0);
	}

	[Fact]
	public void DataSubjectCount_ReturnsZero_WhenEmpty()
	{
		// Assert
		_sut.DataSubjectCount.ShouldBe(0);
	}

	#endregion

	#region Helpers

	private static DataLocationRegistration CreateRegistration(
		string tableName = "Users",
		string fieldName = "Email")
	{
		return new DataLocationRegistration
		{
			TableName = tableName,
			FieldName = fieldName,
			IdType = DataSubjectIdType.Email,
			DataCategory = "ContactInfo",
			DataSubjectIdColumn = "UserId",
			KeyIdColumn = "EncryptionKeyId",
			Description = "Test registration"
		};
	}

	private static DataLocation CreateDataLocation(
		string tableName = "Users",
		string fieldName = "Email",
		string recordId = "record-1")
	{
		return new DataLocation
		{
			TableName = tableName,
			FieldName = fieldName,
			RecordId = recordId,
			DataCategory = "ContactInfo",
			KeyId = "key-123",
			IsAutoDiscovered = false
		};
	}

	#endregion
}
