// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IComplianceMetrics _metrics;
	private readonly MigrationOptions _options;
	private readonly MigrationService _sut;

	public MigrationServiceShould()
	{
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_metrics = A.Fake<IComplianceMetrics>();
		_options = new MigrationOptions
		{
			TargetVersion = EncryptionVersion.Version11,
			EnableLazyReEncryption = true,
			TrackProgress = true
		};
		_sut = new MigrationService(
			_encryptionProvider,
			_metrics,
			NullLogger<MigrationService>.Instance,
			Microsoft.Extensions.Options.Options.Create(_options));
	}

	[Fact]
	public void ExposeCurrentVersion()
	{
		_sut.CurrentVersion.ShouldBe(EncryptionVersion.Version11);
	}

	[Fact]
	public void DetectUnknownVersionForShortCiphertext()
	{
		// Arrange - less than 28 bytes minimum
		var shortData = new byte[10];

		// Act
		var version = _sut.DetectVersion(shortData);

		// Assert
		version.ShouldBe(EncryptionVersion.Unknown);
	}

	[Fact]
	public void DetectV10ForRawCiphertext()
	{
		// Arrange - 28+ bytes without magic header
		var data = new byte[30];
		data[0] = 0x00; // Not the v1.1 magic byte

		// Act
		var version = _sut.DetectVersion(data);

		// Assert
		version.ShouldBe(EncryptionVersion.Version10);
	}

	[Fact]
	public void DetectV11ForMagicByteHeader()
	{
		// Arrange - v1.1 format: [magic=0xED] [major] [minor] [key_version(4)] [nonce(12)] [data...] [tag(16)]
		var data = new byte[28 + 7]; // MinCiphertextSize + V11HeaderSize
		data[0] = 0xED; // V11MagicByte
		data[1] = 1;    // major
		data[2] = 1;    // minor

		// Act
		var version = _sut.DetectVersion(data);

		// Assert
		version.Major.ShouldBe(1);
		version.Minor.ShouldBe(1);
	}

	[Fact]
	public void ReturnFalseForRequiresMigrationWhenLazyDisabled()
	{
		// Arrange
		var options = new MigrationOptions { EnableLazyReEncryption = false };
		var sut = new MigrationService(
			_encryptionProvider, _metrics,
			NullLogger<MigrationService>.Instance,
			Microsoft.Extensions.Options.Options.Create(options));
		var data = new byte[30];

		// Act
		var result = sut.RequiresMigration(data);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForRequiresMigrationWhenVersionOutdated()
	{
		// Arrange - v1.0 data (no magic byte, 28+ bytes)
		var data = new byte[30];
		data[0] = 0x00;

		// Act
		var result = _sut.RequiresMigration(data);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForRequiresMigrationWhenUnknownVersion()
	{
		// Arrange - too short to detect
		var data = new byte[10];

		// Act
		var result = _sut.RequiresMigration(data);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFailedResultForUnknownVersionMigration()
	{
		// Arrange
		var data = new byte[10]; // too short to detect version

		// Act
		var result = await _sut.MigrateAsync(data, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Unable to detect");
	}

	[Fact]
	public async Task ReturnNotRequiredForAlreadyCurrentVersion()
	{
		// Arrange - v1.1 format
		var data = new byte[35];
		data[0] = 0xED;
		data[1] = 1;
		data[2] = 1;

		// Act
		var result = await _sut.MigrateAsync(data, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.SourceVersion.ShouldBe(result.TargetVersion);
	}

	[Fact]
	public async Task ThrowWhenCiphertextIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.MigrateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateBatchWithEmptyList()
	{
		// Arrange
		var items = Enumerable.Empty<MigrationItem>();

		// Act
		var result = await _sut.MigrateBatchAsync(items, CancellationToken.None);

		// Assert
		result.TotalItems.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowWhenBatchItemsIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.MigrateBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void GetProgressReturnsStats()
	{
		// Act
		var progress = _sut.GetProgress();

		// Assert
		progress.ShouldNotBeNull();
		progress.TotalItemsDetected.ShouldBe(0);
		progress.ItemsMigrated.ShouldBe(0);
		progress.FailureCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowWhenEncryptionProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MigrationService(
				null!,
				_metrics,
				NullLogger<MigrationService>.Instance,
				Microsoft.Extensions.Options.Options.Create(new MigrationOptions())));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MigrationService(
				_encryptionProvider,
				_metrics,
				null!,
				Microsoft.Extensions.Options.Options.Create(new MigrationOptions())));
	}

	[Fact]
	public void AcceptNullMetrics()
	{
		// Act - should not throw
		var sut = new MigrationService(
			_encryptionProvider,
			null,
			NullLogger<MigrationService>.Instance,
			Microsoft.Extensions.Options.Options.Create(new MigrationOptions()));

		// Assert
		sut.ShouldNotBeNull();
	}
}
