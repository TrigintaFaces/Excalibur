// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="InMemoryDataInventoryStore"/>.
/// Tests data registration, discovery, and data map functionality per ADR-054.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class InMemoryDataInventoryStoreShould
{
	private readonly InMemoryDataInventoryStore _sut;

	public InMemoryDataInventoryStoreShould()
	{
		_sut = new InMemoryDataInventoryStore();
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

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_FiltersByTenantId()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Users", "Email") with { TenantIdColumn = "tenant-1" },
			CancellationToken.None).ConfigureAwait(false);
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Customers", "Email") with { TenantIdColumn = "tenant-2" },
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: "tenant-1",
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
	}

	[Fact]
	public async Task FindRegistrationsForDataSubjectAsync_IncludesRegistrationsWithNullTenantColumn()
	{
		// Arrange
		await _sut.SaveRegistrationAsync(
			CreateRegistration("Users", "Email") with { TenantIdColumn = null },
			CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _sut.FindRegistrationsForDataSubjectAsync(
			"test@example.com",
			DataSubjectIdType.Email,
			tenantId: "any-tenant",
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(1);
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
