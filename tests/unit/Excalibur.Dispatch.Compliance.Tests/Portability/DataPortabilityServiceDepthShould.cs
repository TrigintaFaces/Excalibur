using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Portability;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DataPortabilityServiceDepthShould
{
	private readonly DataPortabilityOptions _options = new();
	private readonly NullLogger<DataPortabilityService> _logger = NullLogger<DataPortabilityService>.Instance;

	[Fact]
	public async Task Export_without_inventory_service_returns_zero_data_size()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var result = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.DataSize.ShouldBe(0);
		result.Status.ShouldBe(ExportStatus.Completed);
		result.Format.ShouldBe(ExportFormat.Json);
		result.ExportId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task Export_with_inventory_service_estimates_data_size()
	{
		var inventoryService = A.Fake<IDataInventoryService>();
		A.CallTo(() => inventoryService.DiscoverAsync(
			A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(new DataInventory
			{
				DataSubjectId = "user-1",
				Locations =
				[
					new DataLocation { TableName = "users", FieldName = "email", DataCategory = "contact", RecordId = "r1", KeyId = "k1" },
					new DataLocation { TableName = "orders", FieldName = "address", DataCategory = "contact", RecordId = "r2", KeyId = "k2" }
				]
			});

		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger,
			inventoryService);

		var result = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None).ConfigureAwait(false);

		result.DataSize.ShouldBe(2048L); // 2 locations * 1024
	}

	[Fact]
	public async Task Export_sets_correct_expiry_from_retention_period()
	{
		_options.RetentionPeriod = TimeSpan.FromDays(14);

		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var result = await sut.ExportAsync("user-1", ExportFormat.Csv, CancellationToken.None).ConfigureAwait(false);

		result.ExpiresAt.ShouldNotBeNull();
		var expectedExpiry = result.CreatedAt.Add(TimeSpan.FromDays(14));
		result.ExpiresAt.Value.ShouldBeInRange(expectedExpiry.AddSeconds(-2), expectedExpiry.AddSeconds(2));
	}

	[Fact]
	public async Task Get_export_status_returns_null_for_unknown_export()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var result = await sut.GetExportStatusAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task Get_export_status_returns_existing_export()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var export = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None).ConfigureAwait(false);

		var status = await sut.GetExportStatusAsync(export.ExportId, CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.ExportId.ShouldBe(export.ExportId);
		status.Status.ShouldBe(ExportStatus.Completed);
	}

	[Fact]
	public async Task Get_export_status_marks_expired_exports()
	{
		_options.RetentionPeriod = TimeSpan.FromMilliseconds(1);

		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		var export = await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None).ConfigureAwait(false);

		// Wait for expiry
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50).ConfigureAwait(false);

		var status = await sut.GetExportStatusAsync(export.ExportId, CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.Status.ShouldBe(ExportStatus.Expired);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_subject_id()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		await Should.ThrowAsync<ArgumentException>(
			() => sut.ExportAsync(null!, ExportFormat.Json, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ExportAsync("", ExportFormat.Json, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ExportAsync("  ", ExportFormat.Json, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_export_id_in_status()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetExportStatusAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetExportStatusAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new DataPortabilityService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new DataPortabilityService(
				Microsoft.Extensions.Options.Options.Create(_options), null!));
	}

	[Fact]
	public async Task Export_with_different_formats()
	{
		var sut = new DataPortabilityService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_logger);

		foreach (var format in Enum.GetValues<ExportFormat>())
		{
			var result = await sut.ExportAsync("user-1", format, CancellationToken.None).ConfigureAwait(false);
			result.Format.ShouldBe(format);
		}
	}
}

