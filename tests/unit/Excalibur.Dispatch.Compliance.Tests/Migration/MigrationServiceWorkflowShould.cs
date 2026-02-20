using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

/// <summary>
/// Tests the full migration workflow including v1.0-to-v1.1 migration,
/// batch processing, progress tracking, and fail-fast behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationServiceWorkflowShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IComplianceMetrics _metrics = A.Fake<IComplianceMetrics>();

	[Fact]
	public async Task Migrate_v10_ciphertext_to_v11_format()
	{
		// Arrange - v1.0 format: raw 28+ byte ciphertext (no magic byte)
		var v10Data = new byte[30];
		v10Data[0] = 0x00; // Not v1.1 magic byte
		// Fill with some data for realistic test
		for (var i = 1; i < 30; i++)
		{
			v10Data[i] = (byte)(i % 256);
		}

		var decrypted = new byte[] { 10, 20, 30 };
		var reEncrypted = new EncryptedData
		{
			Ciphertext = [40, 50, 60],
			Iv = new byte[12],
			KeyId = "k1",
			KeyVersion = 2,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(decrypted));
		A.CallTo(() => _encryptionProvider.EncryptAsync(
				decrypted, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(reEncrypted));

		var sut = CreateService();

		// Act
		var result = await sut.MigrateAsync(v10Data, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.SourceVersion.ShouldBe(EncryptionVersion.Version10);
		result.TargetVersion.ShouldBe(EncryptionVersion.Version11);
		result.MigratedCiphertext.ShouldNotBeNull();
		result.MigratedCiphertext!.Length.ShouldBeGreaterThan(0);
		// Verify the migrated ciphertext starts with v1.1 magic byte
		result.MigratedCiphertext[0].ShouldBe((byte)0xED);
		result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task Track_progress_after_successful_migration()
	{
		// Arrange
		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		var decrypted = new byte[] { 1 };
		A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(decrypted));
		A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [2],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		var sut = CreateService();

		// Act
		await sut.MigrateAsync(v10Data, CancellationToken.None).ConfigureAwait(false);
		var progress = sut.GetProgress();

		// Assert
		progress.ItemsMigrated.ShouldBe(1);
		progress.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task Track_failure_in_progress_when_migration_fails()
	{
		// Arrange
		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Key unavailable"));

		var sut = CreateService();

		// Act
		var result = await sut.MigrateAsync(v10Data, CancellationToken.None)
			.ConfigureAwait(false);
		var progress = sut.GetProgress();

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Key unavailable");
		progress.FailureCount.ShouldBe(1);
	}

	[Fact]
	public async Task Batch_migrate_multiple_items_with_mixed_results()
	{
		// Arrange
		var v10Item = new byte[30];
		v10Item[0] = 0x00;
		var shortItem = new byte[10]; // Too short, unknown version

		var items = new List<MigrationItem>
		{
			new("item-1", v10Item),
			new("item-2", shortItem),
		};

		var decrypted = new byte[] { 1 };
		A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(decrypted));
		A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [2],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		var sut = CreateService();

		// Act
		var result = await sut.MigrateBatchAsync(items, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.TotalItems.ShouldBe(2);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.TotalDuration.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task Batch_migrate_skips_already_current_version()
	{
		// Arrange - v1.1 format data (magic byte + version + key version + enough payload)
		var v11Data = new byte[35];
		v11Data[0] = 0xED; // v1.1 magic
		v11Data[1] = 1;    // major
		v11Data[2] = 1;    // minor

		var items = new List<MigrationItem>
		{
			new("already-v11", v11Data),
		};

		var sut = CreateService();

		// Act
		var result = await sut.MigrateBatchAsync(items, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.TotalItems.ShouldBe(1);
		result.SkippedCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public void Detect_v10_version_for_raw_ciphertext()
	{
		// Arrange
		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		var sut = CreateService();

		// Act
		var version = sut.DetectVersion(v10Data);

		// Assert
		version.ShouldBe(EncryptionVersion.Version10);
	}

	[Fact]
	public void Detect_v11_version_for_magic_byte_header()
	{
		// Arrange - needs MinCiphertextSize + V11HeaderSize bytes
		var v11Data = new byte[35]; // 28 + 7
		v11Data[0] = 0xED;
		v11Data[1] = 1;
		v11Data[2] = 1;

		var sut = CreateService();

		// Act
		var version = sut.DetectVersion(v11Data);

		// Assert
		version.Major.ShouldBe(1);
		version.Minor.ShouldBe(1);
	}

	[Fact]
	public void Detect_unknown_version_for_data_too_short()
	{
		var sut = CreateService();
		var result = sut.DetectVersion(new byte[10]);
		result.ShouldBe(EncryptionVersion.Unknown);
	}

	[Fact]
	public void Return_true_for_requires_migration_when_v10_detected()
	{
		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		var sut = CreateService();
		sut.RequiresMigration(v10Data).ShouldBeTrue();
	}

	[Fact]
	public void Return_false_for_requires_migration_when_lazy_disabled()
	{
		var options = new MigrationOptions
		{
			EnableLazyReEncryption = false,
			TargetVersion = EncryptionVersion.Version11,
		};
		var sut = new MigrationService(
			_encryptionProvider, _metrics,
			NullLogger<MigrationService>.Instance,
			Microsoft.Extensions.Options.Options.Create(options));

		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		sut.RequiresMigration(v10Data).ShouldBeFalse();
	}

	[Fact]
	public void Return_false_for_requires_migration_when_already_at_target()
	{
		var v11Data = new byte[35];
		v11Data[0] = 0xED;
		v11Data[1] = 1;
		v11Data[2] = 1;

		var sut = CreateService();
		sut.RequiresMigration(v11Data).ShouldBeFalse();
	}

	[Fact]
	public async Task Migration_records_metrics()
	{
		// Arrange
		var v10Data = new byte[30];
		v10Data[0] = 0x00;

		var decrypted = new byte[] { 1 };
		A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(decrypted));
		A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [2],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		var sut = CreateService();

		// Act
		await sut.MigrateAsync(v10Data, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _metrics.RecordEncryptionOperation(
				"ReEncrypt", "Migration", v10Data.Length))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Track_version_distribution_in_progress()
	{
		var sut = CreateService();

		// Detect v1.0
		var v10Data = new byte[30];
		v10Data[0] = 0x00;
		sut.DetectVersion(v10Data);

		// Detect v1.1
		var v11Data = new byte[35];
		v11Data[0] = 0xED;
		v11Data[1] = 1;
		v11Data[2] = 1;
		sut.DetectVersion(v11Data);

		var progress = sut.GetProgress();
		progress.VersionDistribution.ShouldNotBeEmpty();
		progress.TotalItemsDetected.ShouldBeGreaterThan(0);
	}

	private MigrationService CreateService(MigrationOptions? options = null) =>
		new(
			_encryptionProvider,
			_metrics,
			NullLogger<MigrationService>.Instance,
			Microsoft.Extensions.Options.Options.Create(options ?? new MigrationOptions
			{
				TargetVersion = EncryptionVersion.Version11,
				EnableLazyReEncryption = true,
				TrackProgress = true,
			}));
}
