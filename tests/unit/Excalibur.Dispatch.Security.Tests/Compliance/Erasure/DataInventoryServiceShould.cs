// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="DataInventoryService"/>.
/// Tests personal data discovery and mapping per ADR-054.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DataInventoryServiceShould
{
	private readonly IDataInventoryStore _store;
	private readonly IDataInventoryQueryStore _queryStore;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly DataInventoryService _sut;

	public DataInventoryServiceShould()
	{
		_store = A.Fake<IDataInventoryStore>();
		_queryStore = A.Fake<IDataInventoryQueryStore>();
		_keyProvider = A.Fake<IKeyManagementProvider>();

		// Wire up GetService to return the query sub-store (required before SUT construction)
		_ = A.CallTo(() => _store.GetService(typeof(IDataInventoryQueryStore)))
			.Returns(_queryStore);

		_sut = new DataInventoryService(_store, _keyProvider, NullLogger<DataInventoryService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenStoreIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataInventoryService(
			null!,
			_keyProvider,
			NullLogger<DataInventoryService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenKeyProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataInventoryService(
			_store,
			null!,
			NullLogger<DataInventoryService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataInventoryService(
			_store,
			_keyProvider,
			null!));
	}

	#endregion Constructor Tests

	#region DiscoverAsync Tests

	[Fact]
	public async Task DiscoverAsync_ThrowsArgumentException_WhenDataSubjectIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.DiscoverAsync(string.Empty, DataSubjectIdType.UserId, null, CancellationToken.None));
	}

	[Fact]
	public async Task DiscoverAsync_ReturnsEmptyInventory_WhenNoDataFound()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<DataLocationRegistration>());

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(
			A<string>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<DataLocation>());

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.DataSubjectId.ShouldBe(DataSubjectHasher.HashDataSubjectId("user-123"));
		result.Locations.ShouldBeEmpty();
		result.AssociatedKeys.ShouldBeEmpty();
		result.HasData.ShouldBeFalse();
	}

	[Fact]
	public async Task DiscoverAsync_ReturnsLocations_WhenDiscoveredLocationsExist()
	{
		// Arrange
		var locations = new[]
		{
			new DataLocation
			{
				TableName = "Users",
				FieldName = "Email",
				DataCategory = "ContactInfo",
				RecordId = "1",
				KeyId = "key-1"
			},
			new DataLocation
			{
				TableName = "Users",
				FieldName = "Phone",
				DataCategory = "ContactInfo",
				RecordId = "1",
				KeyId = "key-1"
			}
		};

		_ = A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			A<string>._,
			A<DataSubjectIdType>._,
			A<string?>._,
			A<CancellationToken>._))
			.Returns(Array.Empty<DataLocationRegistration>());

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(
			A<string>._,
			A<CancellationToken>._))
			.Returns(locations);

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Purpose = "USER"
			});

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.Locations.Count.ShouldBe(2);
		result.HasData.ShouldBeTrue();
	}

	[Fact]
	public async Task DiscoverAsync_MapsKeyReferences_FromLocations()
	{
		// Arrange
		var locations = new[]
		{
			new DataLocation { TableName = "Users", FieldName = "Email", DataCategory = "Contact", RecordId = "1", KeyId = "key-1" },
			new DataLocation { TableName = "Users", FieldName = "SSN", DataCategory = "PII", RecordId = "1", KeyId = "key-2" },
			new DataLocation { TableName = "Orders", FieldName = "Address", DataCategory = "Contact", RecordId = "1", KeyId = "key-1" }
		};

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(locations);

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Purpose = "USER"
			});

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("key-2", A<CancellationToken>._))
			.Returns(new KeyMetadata
			{
				KeyId = "key-2",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
				Purpose = "FIELD"
			});

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.AssociatedKeys.Count.ShouldBe(2);
		result.AssociatedKeys.ShouldContain(k => k.KeyId == "key-1" && k.RecordCount == 2);
		result.AssociatedKeys.ShouldContain(k => k.KeyId == "key-2" && k.RecordCount == 1);
	}

	[Fact]
	public async Task DiscoverAsync_MapsKeyType_FromPurpose()
	{
		// Arrange
		var locations = new[]
		{
			new DataLocation { TableName = "T1", FieldName = "F1", DataCategory = "C", RecordId = "1", KeyId = "user-key" },
			new DataLocation { TableName = "T2", FieldName = "F2", DataCategory = "C", RecordId = "1", KeyId = "tenant-key" },
			new DataLocation { TableName = "T3", FieldName = "F3", DataCategory = "C", RecordId = "1", KeyId = "field-key" }
		};

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(locations);

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("user-key", A<CancellationToken>._))
			.Returns(CreateKeyMetadata("user-key", "USER"));

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("tenant-key", A<CancellationToken>._))
			.Returns(CreateKeyMetadata("tenant-key", "TENANT"));

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("field-key", A<CancellationToken>._))
			.Returns(CreateKeyMetadata("field-key", "FIELD"));

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.AssociatedKeys.ShouldContain(k => k.KeyId == "user-key" && k.KeyScope == EncryptionKeyScope.User);
		result.AssociatedKeys.ShouldContain(k => k.KeyId == "tenant-key" && k.KeyScope == EncryptionKeyScope.Tenant);
		result.AssociatedKeys.ShouldContain(k => k.KeyId == "field-key" && k.KeyScope == EncryptionKeyScope.Field);
	}

	[Fact]
	public async Task DiscoverAsync_SkipsKeys_WhenKeyNotFound()
	{
		// Arrange
		var locations = new[]
		{
			new DataLocation { TableName = "T1", FieldName = "F1", DataCategory = "C", RecordId = "1", KeyId = "missing-key" }
		};

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(locations);

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("missing-key", A<CancellationToken>._))
			.Returns((KeyMetadata?)null);

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.Locations.Count.ShouldBe(1);
		result.AssociatedKeys.ShouldBeEmpty(); // Key not found, so no reference added
	}

	[Fact]
	public async Task DiscoverAsync_ContinuesOnKeyError()
	{
		// Arrange
		var locations = new[]
		{
			new DataLocation { TableName = "T1", FieldName = "F1", DataCategory = "C", RecordId = "1", KeyId = "error-key" },
			new DataLocation { TableName = "T2", FieldName = "F2", DataCategory = "C", RecordId = "1", KeyId = "good-key" }
		};

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(locations);

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("error-key", A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Key error"));

		_ = A.CallTo(() => _keyProvider.GetKeyAsync("good-key", A<CancellationToken>._))
			.Returns(CreateKeyMetadata("good-key", "USER"));

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		// Assert
		result.AssociatedKeys.Count.ShouldBe(1);
		result.AssociatedKeys[0].KeyId.ShouldBe("good-key");
	}

	[Fact]
	public async Task DiscoverAsync_SetsDiscoveredTimestamp()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Array.Empty<DataLocation>());

		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, null, CancellationToken.None);

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.DiscoveredAt.ShouldBeInRange(before, after);
	}

	[Fact]
	public async Task DiscoverAsync_FiltersByTenant_WhenProvided()
	{
		// Arrange
		const string tenantId = "tenant-123";

		_ = A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Array.Empty<DataLocation>());

		// Act
		_ = await _sut.DiscoverAsync("user-123", DataSubjectIdType.UserId, tenantId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			"user-123",
			DataSubjectIdType.UserId,
			tenantId,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion DiscoverAsync Tests

	#region RegisterDataLocationAsync Tests

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentNullException_WhenRegistrationIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RegisterDataLocationAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentException_WhenTableNameIsEmpty()
	{
		// Arrange
		var registration = CreateValidRegistration() with { TableName = string.Empty };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentException_WhenFieldNameIsEmpty()
	{
		// Arrange
		var registration = CreateValidRegistration() with { FieldName = string.Empty };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentException_WhenDataCategoryIsEmpty()
	{
		// Arrange
		var registration = CreateValidRegistration() with { DataCategory = string.Empty };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentException_WhenDataSubjectIdColumnIsEmpty()
	{
		// Arrange
		var registration = CreateValidRegistration() with { DataSubjectIdColumn = string.Empty };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_ThrowsArgumentException_WhenKeyIdColumnIsEmpty()
	{
		// Arrange
		var registration = CreateValidRegistration() with { KeyIdColumn = string.Empty };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterDataLocationAsync_SavesRegistration_WhenValid()
	{
		// Arrange
		var registration = CreateValidRegistration();

		// Act
		await _sut.RegisterDataLocationAsync(registration, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.SaveRegistrationAsync(
			registration,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion RegisterDataLocationAsync Tests

	#region UnregisterDataLocationAsync Tests

	[Fact]
	public async Task UnregisterDataLocationAsync_ThrowsArgumentException_WhenTableNameIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.UnregisterDataLocationAsync(string.Empty, "FieldName", CancellationToken.None));
	}

	[Fact]
	public async Task UnregisterDataLocationAsync_ThrowsArgumentException_WhenFieldNameIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.UnregisterDataLocationAsync("TableName", string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task UnregisterDataLocationAsync_RemovesRegistration_WhenExists()
	{
		// Arrange
		_ = A.CallTo(() => _store.RemoveRegistrationAsync("Users", "Email", A<CancellationToken>._))
			.Returns(true);

		// Act
		await _sut.UnregisterDataLocationAsync("Users", "Email", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _store.RemoveRegistrationAsync("Users", "Email", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UnregisterDataLocationAsync_CompletesSuccessfully_WhenNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _store.RemoveRegistrationAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);

		// Act & Assert - should not throw
		await _sut.UnregisterDataLocationAsync("NonExistent", "Field", CancellationToken.None);
	}

	#endregion UnregisterDataLocationAsync Tests

	#region GetDataMapAsync Tests

	[Fact]
	public async Task GetDataMapAsync_ReturnsEmptyMap_WhenNoEntries()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.GetDataMapEntriesAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Array.Empty<DataMapEntry>());

		// Act
		var result = await _sut.GetDataMapAsync(null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Entries.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDataMapAsync_ReturnsEntries_WhenExist()
	{
		// Arrange
		var entries = new[]
		{
			new DataMapEntry
			{
				TableName = "Users",
				FieldName = "Email",
				DataCategory = "ContactInfo",
				IsAutoDiscovered = true,
				RecordCount = 1000
			},
			new DataMapEntry
			{
				TableName = "Users",
				FieldName = "SSN",
				DataCategory = "PII",
				IsAutoDiscovered = false,
				RecordCount = 1000
			}
		};

		_ = A.CallTo(() => _queryStore.GetDataMapEntriesAsync(A<string?>._, A<CancellationToken>._))
			.Returns(entries);

		// Act
		var result = await _sut.GetDataMapAsync(null, CancellationToken.None);

		// Assert
		result.Entries.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetDataMapAsync_PassesTenantFilter()
	{
		// Arrange
		const string tenantId = "tenant-123";
		_ = A.CallTo(() => _queryStore.GetDataMapEntriesAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Array.Empty<DataMapEntry>());

		// Act
		_ = await _sut.GetDataMapAsync(tenantId, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _queryStore.GetDataMapEntriesAsync(tenantId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetDataMapAsync_SetsGeneratedAt()
	{
		// Arrange
		_ = A.CallTo(() => _queryStore.GetDataMapEntriesAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Array.Empty<DataMapEntry>());

		var before = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.GetDataMapAsync(null, CancellationToken.None);

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.GeneratedAt.ShouldBeInRange(before, after);
	}

	#endregion GetDataMapAsync Tests

	#region Helper Methods

	private static DataLocationRegistration CreateValidRegistration()
	{
		return new DataLocationRegistration
		{
			TableName = "Users",
			FieldName = "Email",
			DataCategory = "ContactInfo",
			DataSubjectIdColumn = "UserId",
			IdType = DataSubjectIdType.UserId,
			KeyIdColumn = "EncryptionKeyId",
			Description = "User email addresses"
		};
	}

	private static KeyMetadata CreateKeyMetadata(string keyId, string purpose)
	{
		return new KeyMetadata
		{
			KeyId = keyId,
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
			Purpose = purpose
		};
	}

	#endregion Helper Methods
}
