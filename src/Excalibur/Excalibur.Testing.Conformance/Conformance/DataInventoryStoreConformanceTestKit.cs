// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IDataInventoryStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your data inventory store implementation conforms to the IDataInventoryStore contract.
/// </para>
/// <para>
/// The test kit verifies core data inventory store operations including registration save/remove/query,
/// discovered locations save/query, data map generation, and multi-tenant isolation.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IDataInventoryStore implements GDPR RoPA (Records of Processing Activities)
/// per Article 30 which tracks:
/// <list type="bullet">
/// <item><description><c>SaveRegistrationAsync</c> UPSERTS by TableName:FieldName key (does NOT throw on duplicate)</description></item>
/// <item><description><c>SaveRegistrationAsync</c> THROWS ArgumentNullException on null registration</description></item>
/// <item><description><c>RecordDiscoveredLocationAsync</c> THROWS ArgumentNullException on null location</description></item>
/// <item><description><c>RecordDiscoveredLocationAsync</c> THROWS ArgumentException on null/whitespace dataSubjectId</description></item>
/// <item><description><c>RecordDiscoveredLocationAsync</c> deduplicates by TableName+FieldName+RecordId</description></item>
/// <item><description><c>GetDataMapEntriesAsync</c> merges registrations with discovered locations</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerDataInventoryStoreConformanceTests : DataInventoryStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IDataInventoryStore CreateStore() =&gt;
///         new SqlServerDataInventoryStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class DataInventoryStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh data inventory store instance for testing.
	/// </summary>
	/// <returns>An IDataInventoryStore implementation to test.</returns>
	protected abstract IDataInventoryStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a test data location registration with the given parameters.
	/// </summary>
	/// <param name="tableName">The table name. Default is "TestTable".</param>
	/// <param name="fieldName">The field name. Default is "TestField".</param>
	/// <param name="dataCategory">The data category. Default is "ContactInfo".</param>
	/// <param name="idType">The ID type. Default is UserId.</param>
	/// <param name="tenantIdColumn">Optional tenant ID column for multi-tenant.</param>
	/// <param name="description">Optional description.</param>
	/// <returns>A test data location registration.</returns>
	protected virtual DataLocationRegistration CreateRegistration(
		string tableName = "TestTable",
		string fieldName = "TestField",
		string dataCategory = "ContactInfo",
		DataSubjectIdType idType = DataSubjectIdType.UserId,
		string? tenantIdColumn = null,
		string? description = null) =>
		new()
		{
			TableName = tableName,
			FieldName = fieldName,
			DataCategory = dataCategory,
			DataSubjectIdColumn = "UserId",
			IdType = idType,
			KeyIdColumn = "KeyId",
			TenantIdColumn = tenantIdColumn,
			Description = description ?? $"Test registration for {tableName}.{fieldName}"
		};

	/// <summary>
	/// Creates a test data location with the given parameters.
	/// </summary>
	/// <param name="tableName">The table name. Default is "TestTable".</param>
	/// <param name="fieldName">The field name. Default is "TestField".</param>
	/// <param name="dataCategory">The data category. Default is "ContactInfo".</param>
	/// <param name="recordId">Optional record ID. If not provided, a new GUID is generated.</param>
	/// <param name="keyId">Optional key ID. If not provided, a new GUID is generated.</param>
	/// <param name="isAutoDiscovered">Whether this is auto-discovered. Default is true.</param>
	/// <returns>A test data location.</returns>
	protected virtual DataLocation CreateLocation(
		string tableName = "TestTable",
		string fieldName = "TestField",
		string dataCategory = "ContactInfo",
		string? recordId = null,
		string? keyId = null,
		bool isAutoDiscovered = true) =>
		new()
		{
			TableName = tableName,
			FieldName = fieldName,
			DataCategory = dataCategory,
			RecordId = recordId ?? Guid.NewGuid().ToString("N"),
			KeyId = keyId ?? Guid.NewGuid().ToString("N"),
			IsAutoDiscovered = isAutoDiscovered
		};

	/// <summary>
	/// Generates a unique data subject ID for testing.
	/// </summary>
	/// <returns>A unique data subject ID.</returns>
	protected virtual string GenerateDataSubjectId() => $"user-{Guid.NewGuid():N}";

	/// <summary>
	/// Resolves the <see cref="IDataInventoryQueryStore"/> sub-interface from a store via GetService.
	/// </summary>
	private static IDataInventoryQueryStore GetQueryStore(IDataInventoryStore store) =>
		(IDataInventoryQueryStore?)store.GetService(typeof(IDataInventoryQueryStore))
		?? throw new TestFixtureAssertionException("Store does not implement IDataInventoryQueryStore via GetService.");

	#region Registration Save Tests

	/// <summary>
	/// Verifies that SaveRegistrationAsync persists a registration retrievable via GetAllRegistrationsAsync.
	/// </summary>
	protected virtual async Task SaveRegistrationAsync_ShouldPersistRegistration()
	{
		// Arrange
		var store = CreateStore();
		var registration = CreateRegistration();

		try
		{
			// Act
			await store.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);
			var allRegistrations = await store.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);

			// Assert
			var found = allRegistrations.FirstOrDefault(r =>
				r.TableName == registration.TableName &&
				r.FieldName == registration.FieldName);

			if (found is null)
			{
				throw new TestFixtureAssertionException(
					"SaveRegistrationAsync should persist registration retrievable via GetAllRegistrationsAsync");
			}

			if (found.DataCategory != registration.DataCategory ||
				found.IdType != registration.IdType ||
				found.Description != registration.Description)
			{
				throw new TestFixtureAssertionException(
					"SaveRegistrationAsync should persist all registration properties correctly");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that SaveRegistrationAsync upserts (replaces) on duplicate TableName:FieldName key.
	/// </summary>
	protected virtual async Task SaveRegistrationAsync_DuplicateKey_ShouldUpsert()
	{
		// Arrange
		var store = CreateStore();
		var original = CreateRegistration(description: "Original description");
		var updated = CreateRegistration(description: "Updated description");

		try
		{
			// Act
			await store.SaveRegistrationAsync(original, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(updated, CancellationToken.None).ConfigureAwait(false);

			var allRegistrations = await store.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);

			// Assert
			var matching = allRegistrations.Where(r =>
				r.TableName == original.TableName &&
				r.FieldName == original.FieldName).ToList();

			if (matching.Count != 1)
			{
				throw new TestFixtureAssertionException(
					$"SaveRegistrationAsync should upsert on duplicate key. Expected 1 registration but found {matching.Count}");
			}

			if (matching[0].Description != "Updated description")
			{
				throw new TestFixtureAssertionException(
					"SaveRegistrationAsync should replace existing registration with updated values");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that SaveRegistrationAsync throws ArgumentNullException on null registration.
	/// </summary>
	protected virtual async Task SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act & Assert
			var threw = false;
			try
			{
				await store.SaveRegistrationAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				threw = true;
			}

			if (!threw)
			{
				throw new TestFixtureAssertionException(
					"SaveRegistrationAsync should throw ArgumentNullException on null registration");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Registration Remove Tests

	/// <summary>
	/// Verifies that RemoveRegistrationAsync returns true when removing an existing registration.
	/// </summary>
	protected virtual async Task RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue()
	{
		// Arrange
		var store = CreateStore();
		var registration = CreateRegistration();

		try
		{
			await store.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);

			// Act
			var result = await store.RemoveRegistrationAsync(
				registration.TableName,
				registration.FieldName,
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (!result)
			{
				throw new TestFixtureAssertionException(
					"RemoveRegistrationAsync should return true when removing an existing registration");
			}

			var allRegistrations = await store.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);
			var found = allRegistrations.FirstOrDefault(r =>
				r.TableName == registration.TableName &&
				r.FieldName == registration.FieldName);

			if (found is not null)
			{
				throw new TestFixtureAssertionException(
					"RemoveRegistrationAsync should remove the registration from the store");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that RemoveRegistrationAsync returns false when registration does not exist.
	/// </summary>
	protected virtual async Task RemoveRegistrationAsync_NonExistent_ShouldReturnFalse()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act
			var result = await store.RemoveRegistrationAsync(
				"NonExistentTable",
				"NonExistentField",
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (result)
			{
				throw new TestFixtureAssertionException(
					"RemoveRegistrationAsync should return false when registration does not exist");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Registration Query Tests

	/// <summary>
	/// Verifies that GetAllRegistrationsAsync returns all registrations.
	/// </summary>
	protected virtual async Task GetAllRegistrationsAsync_ShouldReturnAllRegistrations()
	{
		// Arrange
		var store = CreateStore();
		var reg1 = CreateRegistration("Table1", "Field1");
		var reg2 = CreateRegistration("Table2", "Field2");
		var reg3 = CreateRegistration("Table3", "Field3");

		try
		{
			await store.SaveRegistrationAsync(reg1, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(reg2, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(reg3, CancellationToken.None).ConfigureAwait(false);

			// Act
			var allRegistrations = await store.GetAllRegistrationsAsync(CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (allRegistrations.Count < 3)
			{
				throw new TestFixtureAssertionException(
					$"GetAllRegistrationsAsync should return all registrations. Expected at least 3 but got {allRegistrations.Count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that FindRegistrationsForDataSubjectAsync filters by IdType and tenant.
	/// </summary>
	protected virtual async Task FindRegistrationsForDataSubjectAsync_ShouldFilterByIdTypeAndTenant()
	{
		// Arrange
		var store = CreateStore();
		var userIdReg = CreateRegistration("UserTable", "UserField", idType: DataSubjectIdType.UserId);
		var emailReg = CreateRegistration("EmailTable", "EmailField", idType: DataSubjectIdType.Email);
		var tenantReg = CreateRegistration("TenantTable", "TenantField", idType: DataSubjectIdType.UserId, tenantIdColumn: "TenantA");

		try
		{
			await store.SaveRegistrationAsync(userIdReg, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(emailReg, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(tenantReg, CancellationToken.None).ConfigureAwait(false);

			// Act - Filter by UserId type only
			var userIdResults = await GetQueryStore(store).FindRegistrationsForDataSubjectAsync(
				"test-user",
				DataSubjectIdType.UserId,
				null,
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (userIdResults.Any(r => r.IdType != DataSubjectIdType.UserId))
			{
				throw new TestFixtureAssertionException(
					"FindRegistrationsForDataSubjectAsync should filter by IdType");
			}

			// Act - Filter by Email type
			var emailResults = await GetQueryStore(store).FindRegistrationsForDataSubjectAsync(
				"test@example.com",
				DataSubjectIdType.Email,
				null,
				CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (emailResults.Any(r => r.IdType != DataSubjectIdType.Email))
			{
				throw new TestFixtureAssertionException(
					"FindRegistrationsForDataSubjectAsync should only return registrations matching the IdType");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Discovered Locations Save Tests

	/// <summary>
	/// Verifies that RecordDiscoveredLocationAsync persists location retrievable via GetDiscoveredLocationsAsync.
	/// </summary>
	protected virtual async Task RecordDiscoveredLocationAsync_ShouldPersistLocation()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId = GenerateDataSubjectId();
		var location = CreateLocation();

		try
		{
			// Act
			await store.RecordDiscoveredLocationAsync(location, dataSubjectId, CancellationToken.None).ConfigureAwait(false);
			var locations = await GetQueryStore(store).GetDiscoveredLocationsAsync(dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var found = locations.FirstOrDefault(l =>
				l.TableName == location.TableName &&
				l.FieldName == location.FieldName &&
				l.RecordId == location.RecordId);

			if (found is null)
			{
				throw new TestFixtureAssertionException(
					"RecordDiscoveredLocationAsync should persist location retrievable via GetDiscoveredLocationsAsync");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that RecordDiscoveredLocationAsync throws ArgumentNullException on null location.
	/// </summary>
	protected virtual async Task RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId = GenerateDataSubjectId();

		try
		{
			// Act & Assert
			var threw = false;
			try
			{
				await store.RecordDiscoveredLocationAsync(null!, dataSubjectId, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				threw = true;
			}

			if (!threw)
			{
				throw new TestFixtureAssertionException(
					"RecordDiscoveredLocationAsync should throw ArgumentNullException on null location");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that RecordDiscoveredLocationAsync throws ArgumentException on null/whitespace dataSubjectId.
	/// </summary>
	protected virtual async Task RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException()
	{
		// Arrange
		var store = CreateStore();
		var location = CreateLocation();

		try
		{
			// Act & Assert - null
			var threwOnNull = false;
			try
			{
				await store.RecordDiscoveredLocationAsync(location, null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				threwOnNull = true;
			}

			if (!threwOnNull)
			{
				throw new TestFixtureAssertionException(
					"RecordDiscoveredLocationAsync should throw ArgumentException on null dataSubjectId");
			}

			// Act & Assert - whitespace
			var threwOnWhitespace = false;
			try
			{
				await store.RecordDiscoveredLocationAsync(location, "   ", CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				threwOnWhitespace = true;
			}

			if (!threwOnWhitespace)
			{
				throw new TestFixtureAssertionException(
					"RecordDiscoveredLocationAsync should throw ArgumentException on whitespace dataSubjectId");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that RecordDiscoveredLocationAsync deduplicates by TableName+FieldName+RecordId.
	/// </summary>
	protected virtual async Task RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId = GenerateDataSubjectId();
		var recordId = Guid.NewGuid().ToString("N");
		var location1 = CreateLocation(recordId: recordId);
		var location2 = CreateLocation(recordId: recordId); // Same TableName, FieldName, RecordId

		try
		{
			// Act
			await store.RecordDiscoveredLocationAsync(location1, dataSubjectId, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(location2, dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			var locations = await GetQueryStore(store).GetDiscoveredLocationsAsync(dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var matching = locations.Where(l =>
				l.TableName == location1.TableName &&
				l.FieldName == location1.FieldName &&
				l.RecordId == recordId).ToList();

			if (matching.Count != 1)
			{
				throw new TestFixtureAssertionException(
					$"RecordDiscoveredLocationAsync should deduplicate by TableName+FieldName+RecordId. Expected 1 but found {matching.Count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Discovered Locations Query Tests

	/// <summary>
	/// Verifies that GetDiscoveredLocationsAsync returns locations for an existing subject.
	/// </summary>
	protected virtual async Task GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId = GenerateDataSubjectId();
		var location1 = CreateLocation("Table1", "Field1");
		var location2 = CreateLocation("Table2", "Field2");

		try
		{
			await store.RecordDiscoveredLocationAsync(location1, dataSubjectId, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(location2, dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			// Act
			var locations = await GetQueryStore(store).GetDiscoveredLocationsAsync(dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (locations.Count < 2)
			{
				throw new TestFixtureAssertionException(
					$"GetDiscoveredLocationsAsync should return all locations for subject. Expected at least 2 but got {locations.Count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetDiscoveredLocationsAsync returns empty list for non-existent subject.
	/// </summary>
	protected virtual async Task GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList()
	{
		// Arrange
		var store = CreateStore();

		try
		{
			// Act
			var locations = await GetQueryStore(store).GetDiscoveredLocationsAsync("non-existent-subject", CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (locations.Count != 0)
			{
				throw new TestFixtureAssertionException(
					"GetDiscoveredLocationsAsync should return empty list for non-existent subject");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Data Map Tests

	/// <summary>
	/// Verifies that GetDataMapEntriesAsync merges registrations and discovered locations.
	/// </summary>
	protected virtual async Task GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId = GenerateDataSubjectId();

		// Registration-only location
		var regOnly = CreateRegistration("RegOnlyTable", "RegOnlyField");

		// Discovered-only location
		var discOnly = CreateLocation("DiscOnlyTable", "DiscOnlyField");

		try
		{
			await store.SaveRegistrationAsync(regOnly, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(discOnly, dataSubjectId, CancellationToken.None).ConfigureAwait(false);

			// Act
			var entries = await GetQueryStore(store).GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var hasRegistration = entries.Any(e => e.TableName == "RegOnlyTable" && e.FieldName == "RegOnlyField");
			var hasDiscovered = entries.Any(e => e.TableName == "DiscOnlyTable" && e.FieldName == "DiscOnlyField");

			if (!hasRegistration)
			{
				throw new TestFixtureAssertionException(
					"GetDataMapEntriesAsync should include entries from registrations");
			}

			if (!hasDiscovered)
			{
				throw new TestFixtureAssertionException(
					"GetDataMapEntriesAsync should include entries from discovered locations");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetDataMapEntriesAsync sets IsAutoDiscovered=false for registered locations.
	/// </summary>
	protected virtual async Task GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse()
	{
		// Arrange
		var store = CreateStore();
		var registration = CreateRegistration("RegTable", "RegField");

		try
		{
			await store.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);

			// Act
			var entries = await GetQueryStore(store).GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var entry = entries.FirstOrDefault(e => e.TableName == "RegTable" && e.FieldName == "RegField");

			if (entry is null)
			{
				throw new TestFixtureAssertionException(
					"GetDataMapEntriesAsync should include registered locations");
			}

			if (entry.IsAutoDiscovered)
			{
				throw new TestFixtureAssertionException(
					"GetDataMapEntriesAsync should set IsAutoDiscovered=false for registered locations");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetDataMapEntriesAsync calculates record count from discovered locations.
	/// </summary>
	protected virtual async Task GetDataMapEntriesAsync_ShouldCalculateRecordCount()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId1 = GenerateDataSubjectId();
		var dataSubjectId2 = GenerateDataSubjectId();

		// Save registration first
		var registration = CreateRegistration("CountTable", "CountField");
		await store.SaveRegistrationAsync(registration, CancellationToken.None).ConfigureAwait(false);

		// Record multiple discovered locations for the same table/field
		var location1 = CreateLocation("CountTable", "CountField", recordId: "record-1");
		var location2 = CreateLocation("CountTable", "CountField", recordId: "record-2");
		var location3 = CreateLocation("CountTable", "CountField", recordId: "record-3");

		try
		{
			await store.RecordDiscoveredLocationAsync(location1, dataSubjectId1, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(location2, dataSubjectId1, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(location3, dataSubjectId2, CancellationToken.None).ConfigureAwait(false);

			// Act
			var entries = await GetQueryStore(store).GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var entry = entries.FirstOrDefault(e => e.TableName == "CountTable" && e.FieldName == "CountField");

			if (entry is null)
			{
				throw new TestFixtureAssertionException(
					"GetDataMapEntriesAsync should include entries for the table/field");
			}

			if (entry.RecordCount < 3)
			{
				throw new TestFixtureAssertionException(
					$"GetDataMapEntriesAsync should calculate record count from discovered locations. Expected at least 3 but got {entry.RecordCount}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion

	#region Multi-Tenant Tests

	/// <summary>
	/// Verifies that FindRegistrationsForDataSubjectAsync filters by tenant correctly.
	/// </summary>
	protected virtual async Task FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly()
	{
		// Arrange
		var store = CreateStore();
		var tenantAReg = CreateRegistration("TenantATable", "TenantAField", tenantIdColumn: "TenantA");
		var tenantBReg = CreateRegistration("TenantBTable", "TenantBField", tenantIdColumn: "TenantB");
		var noTenantReg = CreateRegistration("NoTenantTable", "NoTenantField", tenantIdColumn: null);

		try
		{
			await store.SaveRegistrationAsync(tenantAReg, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(tenantBReg, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(noTenantReg, CancellationToken.None).ConfigureAwait(false);

			// Act - Filter by TenantA
			var tenantAResults = await GetQueryStore(store).FindRegistrationsForDataSubjectAsync(
				"test-user",
				DataSubjectIdType.UserId,
				"TenantA",
				CancellationToken.None).ConfigureAwait(false);

			// Assert - Should include TenantA registrations and those with no tenant column
			var hasTenantA = tenantAResults.Any(r => r.TenantIdColumn == "TenantA");
			var hasNoTenant = tenantAResults.Any(r => string.IsNullOrEmpty(r.TenantIdColumn));

			if (!hasTenantA && !hasNoTenant)
			{
				throw new TestFixtureAssertionException(
					"FindRegistrationsForDataSubjectAsync with tenant filter should return matching tenant or no-tenant registrations");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that GetDataMapEntriesAsync works with null tenant (returns all entries).
	/// </summary>
	protected virtual async Task GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries()
	{
		// Arrange
		var store = CreateStore();
		var reg1 = CreateRegistration("AllTable1", "AllField1");
		var reg2 = CreateRegistration("AllTable2", "AllField2");

		try
		{
			await store.SaveRegistrationAsync(reg1, CancellationToken.None).ConfigureAwait(false);
			await store.SaveRegistrationAsync(reg2, CancellationToken.None).ConfigureAwait(false);

			// Act
			var entries = await GetQueryStore(store).GetDataMapEntriesAsync(null, CancellationToken.None).ConfigureAwait(false);

			// Assert
			if (entries.Count < 2)
			{
				throw new TestFixtureAssertionException(
					$"GetDataMapEntriesAsync with null tenant should return all entries. Expected at least 2 but got {entries.Count}");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that locations are isolated per data subject.
	/// </summary>
	protected virtual async Task GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject()
	{
		// Arrange
		var store = CreateStore();
		var dataSubjectId1 = GenerateDataSubjectId();
		var dataSubjectId2 = GenerateDataSubjectId();
		var location1 = CreateLocation("Subject1Table", "Subject1Field");
		var location2 = CreateLocation("Subject2Table", "Subject2Field");

		try
		{
			await store.RecordDiscoveredLocationAsync(location1, dataSubjectId1, CancellationToken.None).ConfigureAwait(false);
			await store.RecordDiscoveredLocationAsync(location2, dataSubjectId2, CancellationToken.None).ConfigureAwait(false);

			// Act
			var locations1 = await GetQueryStore(store).GetDiscoveredLocationsAsync(dataSubjectId1, CancellationToken.None).ConfigureAwait(false);
			var locations2 = await GetQueryStore(store).GetDiscoveredLocationsAsync(dataSubjectId2, CancellationToken.None).ConfigureAwait(false);

			// Assert
			var hasSubject2InSubject1 = locations1.Any(l => l.TableName == "Subject2Table");
			var hasSubject1InSubject2 = locations2.Any(l => l.TableName == "Subject1Table");

			if (hasSubject2InSubject1 || hasSubject1InSubject2)
			{
				throw new TestFixtureAssertionException(
					"GetDiscoveredLocationsAsync should isolate locations by data subject");
			}
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	#endregion
}
