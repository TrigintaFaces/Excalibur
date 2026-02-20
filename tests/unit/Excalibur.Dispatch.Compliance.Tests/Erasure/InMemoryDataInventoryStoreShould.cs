namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class InMemoryDataInventoryStoreShould
{
    private readonly InMemoryDataInventoryStore _sut = new();

    private static DataLocationRegistration CreateRegistration(string? tableName = null, string? fieldName = null) => new()
    {
        TableName = tableName ?? "Users",
        FieldName = fieldName ?? "Email",
        DataCategory = "ContactInfo",
        DataSubjectIdColumn = "UserId",
        KeyIdColumn = "EncryptionKeyId",
        IdType = DataSubjectIdType.UserId
    };

    [Fact]
    public async Task Save_and_retrieve_registration()
    {
        var reg = CreateRegistration();
        await _sut.SaveRegistrationAsync(reg, CancellationToken.None);

        var all = await _sut.GetAllRegistrationsAsync(CancellationToken.None);

        all.Count.ShouldBe(1);
        all[0].TableName.ShouldBe("Users");
    }

    [Fact]
    public async Task Remove_registration()
    {
        await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None);

        var removed = await _sut.RemoveRegistrationAsync("Users", "Email", CancellationToken.None);

        removed.ShouldBeTrue();
        _sut.RegistrationCount.ShouldBe(0);
    }

    [Fact]
    public async Task Return_false_when_removing_nonexistent_registration()
    {
        var removed = await _sut.RemoveRegistrationAsync("Nonexistent", "Field", CancellationToken.None);

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task Find_registrations_by_data_subject_type()
    {
        await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None);

        var results = await _sut.FindRegistrationsForDataSubjectAsync(
            "user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Record_and_retrieve_discovered_locations()
    {
        var location = new DataLocation
        {
            TableName = "Users",
            FieldName = "Email",
            RecordId = "rec-1",
            KeyId = "key-1",
            DataCategory = "ContactInfo",
            IsAutoDiscovered = true
        };

        await _sut.RecordDiscoveredLocationAsync(location, "user-1", CancellationToken.None);

        var results = await _sut.GetDiscoveredLocationsAsync("user-1", CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].TableName.ShouldBe("Users");
    }

    [Fact]
    public async Task Not_duplicate_same_discovered_location()
    {
        var location = new DataLocation
        {
            TableName = "Users",
            FieldName = "Email",
            RecordId = "rec-1",
            KeyId = "key-1",
            DataCategory = "ContactInfo"
        };

        await _sut.RecordDiscoveredLocationAsync(location, "user-1", CancellationToken.None);
        await _sut.RecordDiscoveredLocationAsync(location, "user-1", CancellationToken.None);

        var results = await _sut.GetDiscoveredLocationsAsync("user-1", CancellationToken.None);

        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Return_empty_list_for_unknown_data_subject()
    {
        var results = await _sut.GetDiscoveredLocationsAsync("unknown", CancellationToken.None);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Return_data_map_entries()
    {
        await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None);

        var entries = await _sut.GetDataMapEntriesAsync(null, CancellationToken.None);

        entries.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Return_query_store_via_get_service()
    {
        var service = _sut.GetService(typeof(IDataInventoryQueryStore));

        service.ShouldNotBeNull();
        service.ShouldBe(_sut);
    }

    [Fact]
    public void Return_null_for_unsupported_service()
    {
        _sut.GetService(typeof(string)).ShouldBeNull();
    }

    [Fact]
    public async Task Track_registration_count()
    {
        _sut.RegistrationCount.ShouldBe(0);

        await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None);

        _sut.RegistrationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Track_data_subject_count()
    {
        var location = new DataLocation
        {
            TableName = "Users",
            FieldName = "Email",
            RecordId = "rec-1",
            KeyId = "key-1",
            DataCategory = "ContactInfo"
        };

        await _sut.RecordDiscoveredLocationAsync(location, "user-1", CancellationToken.None);

        _sut.DataSubjectCount.ShouldBe(1);
    }

    [Fact]
    public async Task Clear_all_data()
    {
        await _sut.SaveRegistrationAsync(CreateRegistration(), CancellationToken.None);

        _sut.Clear();

        _sut.RegistrationCount.ShouldBe(0);
        _sut.DataSubjectCount.ShouldBe(0);
    }

    [Fact]
    public void Throw_when_saving_null_registration()
    {
        Should.Throw<ArgumentNullException>(
            () => _sut.SaveRegistrationAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_recording_null_location()
    {
        Should.Throw<ArgumentNullException>(
            () => _sut.RecordDiscoveredLocationAsync(null!, "user-1", CancellationToken.None));
    }

    [Fact]
    public void Throw_when_get_service_null_type()
    {
        Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
    }
}
