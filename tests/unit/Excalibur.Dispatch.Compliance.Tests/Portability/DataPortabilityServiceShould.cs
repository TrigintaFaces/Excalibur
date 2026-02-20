using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Portability;

public class DataPortabilityServiceShould
{
    private readonly DataPortabilityService _sut;
    private readonly DataPortabilityOptions _options = new();

    public DataPortabilityServiceShould()
    {
        _sut = new DataPortabilityService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<DataPortabilityService>.Instance);
    }

    [Fact]
    public async Task Export_data_with_completed_status()
    {
        var result = await _sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None);

        result.ShouldNotBeNull();
        result.ExportId.ShouldNotBeNullOrWhiteSpace();
        result.Format.ShouldBe(ExportFormat.Json);
        result.Status.ShouldBe(ExportStatus.Completed);
    }

    [Fact]
    public async Task Set_expiration_based_on_options()
    {
        var result = await _sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None);

        result.ExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_export_status()
    {
        var exported = await _sut.ExportAsync("user-1", ExportFormat.Csv, CancellationToken.None);

        var status = await _sut.GetExportStatusAsync(exported.ExportId, CancellationToken.None);

        status.ShouldNotBeNull();
        status.ExportId.ShouldBe(exported.ExportId);
    }

    [Fact]
    public async Task Return_null_for_unknown_export()
    {
        var status = await _sut.GetExportStatusAsync("unknown-id", CancellationToken.None);

        status.ShouldBeNull();
    }

    [Fact]
    public async Task Return_expired_status_for_expired_export()
    {
        var shortOptions = new DataPortabilityOptions { RetentionPeriod = TimeSpan.FromMilliseconds(1) };
        var sut = new DataPortabilityService(
            Microsoft.Extensions.Options.Options.Create(shortOptions),
            NullLogger<DataPortabilityService>.Instance);

        var exported = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None);
        await Task.Delay(50); // Wait for expiration

        var status = await sut.GetExportStatusAsync(exported.ExportId, CancellationToken.None);

        status.ShouldNotBeNull();
        status.Status.ShouldBe(ExportStatus.Expired);
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ExportAsync(null!, ExportFormat.Json, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_getting_status_with_null_id()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetExportStatusAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new DataPortabilityService(null!, NullLogger<DataPortabilityService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new DataPortabilityService(Microsoft.Extensions.Options.Options.Create(new DataPortabilityOptions()), null!));
    }

    [Fact]
    public async Task Use_data_inventory_when_available()
    {
        var inventoryService = A.Fake<IDataInventoryService>();
        A.CallTo(() => inventoryService.DiscoverAsync(
            A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
            .Returns(new DataInventory
            {
                DataSubjectId = "hash",
                Locations = [new DataLocation { TableName = "T", FieldName = "F", DataCategory = "C", RecordId = "r1", KeyId = "k1" }],
                AssociatedKeys = [],
                DiscoveredAt = DateTimeOffset.UtcNow
            });

        var sut = new DataPortabilityService(
            Microsoft.Extensions.Options.Options.Create(new DataPortabilityOptions()),
            NullLogger<DataPortabilityService>.Instance,
            inventoryService);

        var result = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None);

        result.DataSize.ShouldBeGreaterThan(0);
    }
}
