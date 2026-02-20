using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class DataInventoryServiceShould
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

		A.CallTo(() => _store.GetService(typeof(IDataInventoryQueryStore)))
			.Returns(_queryStore);

		_sut = new DataInventoryService(
			_store,
			_keyProvider,
			NullLogger<DataInventoryService>.Instance);
	}

	private static DataLocation CreateLocation(string tableName = "Users", string fieldName = "Email", string keyId = "key-1") =>
		new()
		{
			TableName = tableName,
			FieldName = fieldName,
			DataCategory = "ContactInfo",
			RecordId = "record-1",
			KeyId = keyId
		};

	private static KeyMetadata CreateKeyMetadata(string keyId = "key-1", string? purpose = "USER", KeyStatus status = KeyStatus.Active) =>
		new()
		{
			KeyId = keyId,
			Version = 1,
			Status = status,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
			Purpose = purpose
		};

	private static DataLocationRegistration CreateRegistration(
		string tableName = "Customers",
		string fieldName = "PhoneNumber",
		string dataCategory = "ContactInfo",
		string dataSubjectIdColumn = "CustomerId",
		string keyIdColumn = "EncryptionKeyId") =>
		new()
		{
			TableName = tableName,
			FieldName = fieldName,
			DataCategory = dataCategory,
			DataSubjectIdColumn = dataSubjectIdColumn,
			IdType = DataSubjectIdType.UserId,
			KeyIdColumn = keyIdColumn
		};

	[Fact]
	public async Task Discover_data_inventory_for_subject()
	{
		var location = CreateLocation();

		A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			"user-1", DataSubjectIdType.UserId, A<string?>._, CancellationToken.None))
			.Returns(new List<DataLocationRegistration>());

		A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync("user-1", CancellationToken.None))
			.Returns(new List<DataLocation> { location });

		A.CallTo(() => _keyProvider.GetKeyAsync("key-1", CancellationToken.None))
			.Returns(CreateKeyMetadata());

		var result = await _sut.DiscoverAsync("user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Locations.Count.ShouldBe(1);
		result.AssociatedKeys.Count.ShouldBe(1);
		result.AssociatedKeys[0].KeyScope.ShouldBe(EncryptionKeyScope.User);
		result.DataSubjectId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Discover_handles_key_lookup_failure_gracefully()
	{
		var location = CreateLocation(keyId: "key-bad");

		A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			A<string>._, A<DataSubjectIdType>._, A<string?>._, CancellationToken.None))
			.Returns(new List<DataLocationRegistration>());

		A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, CancellationToken.None))
			.Returns(new List<DataLocation> { location });

		A.CallTo(() => _keyProvider.GetKeyAsync("key-bad", CancellationToken.None))
			.Throws(new InvalidOperationException("Key provider error"));

		var result = await _sut.DiscoverAsync("user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Locations.Count.ShouldBe(1);
		result.AssociatedKeys.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Throw_when_discover_data_subject_id_is_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.DiscoverAsync("", DataSubjectIdType.UserId, null, CancellationToken.None));
	}

	[Fact]
	public async Task Register_data_location_with_valid_registration()
	{
		var registration = CreateRegistration();

		await _sut.RegisterDataLocationAsync(registration, CancellationToken.None);

		A.CallTo(() => _store.SaveRegistrationAsync(registration, CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_when_register_null_registration()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.RegisterDataLocationAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_registration_table_name_empty()
	{
		var registration = CreateRegistration(tableName: "");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_registration_field_name_empty()
	{
		var registration = CreateRegistration(fieldName: "");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_registration_data_category_empty()
	{
		var registration = CreateRegistration(dataCategory: "");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_registration_subject_id_column_empty()
	{
		var registration = CreateRegistration(dataSubjectIdColumn: "");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_registration_key_id_column_empty()
	{
		var registration = CreateRegistration(keyIdColumn: "");

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RegisterDataLocationAsync(registration, CancellationToken.None));
	}

	[Fact]
	public async Task Unregister_data_location()
	{
		A.CallTo(() => _store.RemoveRegistrationAsync("Users", "Email", CancellationToken.None))
			.Returns(true);

		await _sut.UnregisterDataLocationAsync("Users", "Email", CancellationToken.None);

		A.CallTo(() => _store.RemoveRegistrationAsync("Users", "Email", CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_when_unregister_table_name_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.UnregisterDataLocationAsync("", "Email", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_unregister_field_name_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.UnregisterDataLocationAsync("Users", "", CancellationToken.None));
	}

	[Fact]
	public async Task Get_data_map_delegates_to_query_store()
	{
		var entries = new List<DataMapEntry>
		{
			new()
			{
				TableName = "Users",
				FieldName = "Email",
				DataCategory = "ContactInfo"
			}
		};

		A.CallTo(() => _queryStore.GetDataMapEntriesAsync("tenant-1", CancellationToken.None))
			.Returns(entries);

		var result = await _sut.GetDataMapAsync("tenant-1", CancellationToken.None);

		result.ShouldNotBeNull();
		result.Entries.Count.ShouldBe(1);
		result.GeneratedAt.ShouldNotBe(default);
	}

	[Fact]
	public void Throw_when_store_is_null()
	{
		Should.Throw<ArgumentNullException>(
			() => new DataInventoryService(
				null!,
				A.Fake<IKeyManagementProvider>(),
				NullLogger<DataInventoryService>.Instance));
	}

	[Fact]
	public void Throw_when_key_provider_is_null()
	{
		var store = A.Fake<IDataInventoryStore>();
		A.CallTo(() => store.GetService(typeof(IDataInventoryQueryStore)))
			.Returns(A.Fake<IDataInventoryQueryStore>());

		Should.Throw<ArgumentNullException>(
			() => new DataInventoryService(
				store,
				null!,
				NullLogger<DataInventoryService>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		var store = A.Fake<IDataInventoryStore>();
		A.CallTo(() => store.GetService(typeof(IDataInventoryQueryStore)))
			.Returns(A.Fake<IDataInventoryQueryStore>());

		Should.Throw<ArgumentNullException>(
			() => new DataInventoryService(
				store,
				A.Fake<IKeyManagementProvider>(),
				null!));
	}

	[Fact]
	public void Throw_when_store_does_not_support_query()
	{
		var store = A.Fake<IDataInventoryStore>();
		A.CallTo(() => store.GetService(typeof(IDataInventoryQueryStore)))
			.Returns(null);

		Should.Throw<InvalidOperationException>(
			() => new DataInventoryService(
				store,
				A.Fake<IKeyManagementProvider>(),
				NullLogger<DataInventoryService>.Instance));
	}

	[Theory]
	[InlineData("USER", EncryptionKeyScope.User)]
	[InlineData("DEK", EncryptionKeyScope.User)]
	[InlineData("TENANT", EncryptionKeyScope.Tenant)]
	[InlineData("KEK", EncryptionKeyScope.Tenant)]
	[InlineData("FIELD", EncryptionKeyScope.Field)]
	[InlineData("UNKNOWN", EncryptionKeyScope.User)]
	[InlineData(null, EncryptionKeyScope.User)]
	public async Task Map_key_scope_correctly_from_purpose(string? purpose, EncryptionKeyScope expectedScope)
	{
		var location = CreateLocation(keyId: "key-scope-test");

		A.CallTo(() => _queryStore.FindRegistrationsForDataSubjectAsync(
			A<string>._, A<DataSubjectIdType>._, A<string?>._, CancellationToken.None))
			.Returns(new List<DataLocationRegistration>());

		A.CallTo(() => _queryStore.GetDiscoveredLocationsAsync(A<string>._, CancellationToken.None))
			.Returns(new List<DataLocation> { location });

		A.CallTo(() => _keyProvider.GetKeyAsync("key-scope-test", CancellationToken.None))
			.Returns(CreateKeyMetadata(keyId: "key-scope-test", purpose: purpose));

		var result = await _sut.DiscoverAsync("user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

		result.AssociatedKeys.Count.ShouldBe(1);
		result.AssociatedKeys[0].KeyScope.ShouldBe(expectedScope);
	}
}
