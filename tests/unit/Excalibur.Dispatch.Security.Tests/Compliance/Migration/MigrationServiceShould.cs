// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Migration;

[Trait("Category", TestCategories.Unit)]
public sealed class MigrationServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IComplianceMetrics _metrics;
	private readonly ILogger<MigrationService> _logger;
	private readonly MigrationOptions _options;

	public MigrationServiceShould()
	{
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_metrics = A.Fake<IComplianceMetrics>();
		_logger = NullLogger<MigrationService>.Instance;
		_options = new MigrationOptions
		{
			TargetVersion = EncryptionVersion.Version11,
			EnableLazyReEncryption = true,
			TrackProgress = true
		};
	}

	private MigrationService CreateSut() =>
		new(_encryptionProvider, _metrics, _logger, Microsoft.Extensions.Options.Options.Create(_options));

	[Fact]
	public void ThrowWhenEncryptionProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MigrationService(null!, _metrics, _logger, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MigrationService(_encryptionProvider, _metrics, null!, Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void AllowNullMetrics()
	{
		// Act & Assert - should not throw
		var sut = new MigrationService(_encryptionProvider, null, _logger, Microsoft.Extensions.Options.Options.Create(_options));
		_ = sut.ShouldNotBeNull();
	}

	[Fact]
	public void ExposeCurrentVersion()
	{
		// Arrange
		var sut = CreateSut();

		// Assert
		sut.CurrentVersion.ShouldBe(EncryptionVersion.Version11);
	}

	[Fact]
	public void DetectVersion_ReturnUnknown_WhenCiphertextTooShort()
	{
		// Arrange
		var sut = CreateSut();
		var shortCiphertext = new byte[10]; // Less than minimum 28 bytes

		// Act
		var version = sut.DetectVersion(shortCiphertext);

		// Assert
		version.ShouldBe(EncryptionVersion.Unknown);
	}

	[Fact]
	public void DetectVersion_ReturnV10_WhenNoHeader()
	{
		// Arrange
		var sut = CreateSut();
		// V1.0 format has no magic byte header - just raw encrypted data
		var v10Ciphertext = new byte[28]; // Minimum size, no magic byte

		// Act
		var version = sut.DetectVersion(v10Ciphertext);

		// Assert
		version.ShouldBe(EncryptionVersion.Version10);
	}

	[Fact]
	public void DetectVersion_ReturnV11_WhenMagicBytePresent()
	{
		// Arrange
		var sut = CreateSut();
		// V1.1 format: [magic(1)=0xED] [major(1)=1] [minor(1)=1] [key_version(4)] [nonce(12)] [ciphertext...] [tag(16)]
		var v11Ciphertext = new byte[35]; // 7 header + 28 minimum
		v11Ciphertext[0] = 0xED; // Magic byte
		v11Ciphertext[1] = 1;    // Major version
		v11Ciphertext[2] = 1;    // Minor version

		// Act
		var version = sut.DetectVersion(v11Ciphertext);

		// Assert
		version.ShouldBe(EncryptionVersion.Version11);
	}

	[Fact]
	public void RequiresMigration_ReturnFalse_WhenLazyReEncryptionDisabled()
	{
		// Arrange
		_options.EnableLazyReEncryption = false;
		var sut = CreateSut();
		var ciphertext = new byte[28];

		// Act
		var result = sut.RequiresMigration(ciphertext);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void RequiresMigration_ReturnFalse_WhenVersionUnknown()
	{
		// Arrange
		var sut = CreateSut();
		var shortCiphertext = new byte[10];

		// Act
		var result = sut.RequiresMigration(shortCiphertext);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void RequiresMigration_ReturnTrue_WhenVersionLessThanTarget()
	{
		// Arrange
		var sut = CreateSut();
		var v10Ciphertext = new byte[28]; // V1.0 format

		// Act
		var result = sut.RequiresMigration(v10Ciphertext);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void RequiresMigration_ReturnFalse_WhenVersionEqualsTarget()
	{
		// Arrange
		var sut = CreateSut();
		var v11Ciphertext = new byte[35];
		v11Ciphertext[0] = 0xED;
		v11Ciphertext[1] = 1;
		v11Ciphertext[2] = 1;

		// Act
		var result = sut.RequiresMigration(v11Ciphertext);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task MigrateAsync_ThrowWhenCiphertextIsNull()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.MigrateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateAsync_ReturnFailed_WhenVersionUnknown()
	{
		// Arrange
		var sut = CreateSut();
		var shortCiphertext = new byte[10];

		// Act
		var result = await sut.MigrateAsync(shortCiphertext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Unable to detect");
	}

	[Fact]
	public async Task MigrateAsync_ReturnNotRequired_WhenVersionIsCurrent()
	{
		// Arrange
		var sut = CreateSut();
		var v11Ciphertext = new byte[35];
		v11Ciphertext[0] = 0xED;
		v11Ciphertext[1] = 1;
		v11Ciphertext[2] = 1;

		// Act
		var result = await sut.MigrateAsync(v11Ciphertext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.SourceVersion.ShouldBe(EncryptionVersion.Version11);
		result.TargetVersion.ShouldBe(EncryptionVersion.Version11);
		result.MigratedCiphertext.ShouldBe(v11Ciphertext); // Same as original
	}

	[Fact]
	public async Task MigrateAsync_PerformMigration_WhenVersionIsOld()
	{
		// Arrange
		var sut = CreateSut();
		var v10Ciphertext = CreateV10Ciphertext("test plaintext");
		var decrypted = "test plaintext"u8.ToArray();

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(decrypted));

		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				decrypted,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = new byte[16],
				KeyId = "key-1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = new byte[12]
			}));

		// Act
		var result = await sut.MigrateAsync(v10Ciphertext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.SourceVersion.ShouldBe(EncryptionVersion.Version10);
		result.TargetVersion.ShouldBe(EncryptionVersion.Version11);
		_ = result.MigratedCiphertext.ShouldNotBeNull();
		result.MigratedCiphertext[0].ShouldBe((byte)0xED); // Magic byte present
	}

	[Fact]
	public async Task MigrateAsync_ReturnFailed_WhenDecryptionFails()
	{
		// Arrange
		var sut = CreateSut();
		var v10Ciphertext = CreateV10Ciphertext("test");

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Throws(new EncryptionException("Decryption failed"));

		// Act
		var result = await sut.MigrateAsync(v10Ciphertext, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Decryption failed");
	}

	[Fact]
	public async Task MigrateBatchAsync_ThrowWhenItemsIsNull()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.MigrateBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MigrateBatchAsync_ProcessAllItems()
	{
		// Arrange
		var sut = CreateSut();
		var v11Bytes1 = new byte[35];
		v11Bytes1[0] = 0xED;
		v11Bytes1[1] = 1;
		v11Bytes1[2] = 1;
		var v11Bytes2 = new byte[35];
		v11Bytes2[0] = 0xED;
		v11Bytes2[1] = 1;
		v11Bytes2[2] = 1;
		var items = new[]
		{
			new MigrationItem("item-1", v11Bytes1), // Already v1.1
			new MigrationItem("item-2", v11Bytes2), // Already v1.1
		};

		// Act
		var result = await sut.MigrateBatchAsync(items, CancellationToken.None);

		// Assert
		result.TotalItems.ShouldBe(2);
		result.SkippedCount.ShouldBe(2); // Both already at target version
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task MigrateBatchAsync_ContinueOnError_WhenFailFastDisabled()
	{
		// Arrange
		_options.FailFast = false;
		var sut = CreateSut();

		var v11Bytes = new byte[35];
		v11Bytes[0] = 0xED;
		v11Bytes[1] = 1;
		v11Bytes[2] = 1;
		var items = new[]
		{
			new MigrationItem("item-1", new byte[10]), // Will fail (too short)
			new MigrationItem("item-2", v11Bytes), // Will succeed
		};

		// Act
		var result = await sut.MigrateBatchAsync(items, CancellationToken.None);

		// Assert
		result.TotalItems.ShouldBe(2);
		result.FailureCount.ShouldBe(1);
		result.SkippedCount.ShouldBe(1);
	}

	[Fact]
	public void GetProgress_ReturnEmptyProgress_Initially()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var progress = sut.GetProgress();

		// Assert
		progress.TotalItemsDetected.ShouldBe(0);
		progress.ItemsMigrated.ShouldBe(0);
		progress.FailureCount.ShouldBe(0);
		progress.CompletionPercentage.ShouldBe(100); // 100% when nothing to do
	}

	[Fact]
	public void GetProgress_TrackVersionDistribution()
	{
		// Arrange
		_options.TrackProgress = true;
		var sut = CreateSut();

		// Act - detect some versions
		var v10 = new byte[28];
		var v11 = new byte[35];
		v11[0] = 0xED;
		v11[1] = 1;
		v11[2] = 1;

		_ = sut.DetectVersion(v10);
		_ = sut.DetectVersion(v10);
		_ = sut.DetectVersion(v11);

		var progress = sut.GetProgress();

		// Assert
		progress.TotalItemsDetected.ShouldBe(3);
		progress.VersionDistribution.ShouldContainKey(EncryptionVersion.Version10);
		progress.VersionDistribution[EncryptionVersion.Version10].ShouldBe(2);
		progress.VersionDistribution.ShouldContainKey(EncryptionVersion.Version11);
		progress.VersionDistribution[EncryptionVersion.Version11].ShouldBe(1);
	}

	private static byte[] CreateV10Ciphertext(string plaintext)
	{
		// V1.0 format: [nonce(12)] [ciphertext(n)] [tag(16)]
		// Just create a fake structure for testing
		var nonce = new byte[12];
		var payload = System.Text.Encoding.UTF8.GetBytes(plaintext);
		var tag = new byte[16];

		var result = new byte[nonce.Length + payload.Length + tag.Length];
		nonce.CopyTo(result, 0);
		payload.CopyTo(result, nonce.Length);
		tag.CopyTo(result, nonce.Length + payload.Length);

		return result;
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionVersionShould
{
	[Fact]
	public void CompareVersionsCorrectly()
	{
		// Assert
		(EncryptionVersion.Version10 < EncryptionVersion.Version11).ShouldBeTrue();
		(EncryptionVersion.Version11 > EncryptionVersion.Version10).ShouldBeTrue();

		// Intentionally testing that <= and >= operators work for identity comparison
		// Note: This is a deliberate self-comparison to validate operator implementation
		var v10 = EncryptionVersion.Version10;
		var v11 = EncryptionVersion.Version11;
#pragma warning disable CS1718 // Comparison made to same variable
		(v10 <= v10).ShouldBeTrue();
		(v11 >= v11).ShouldBeTrue();
#pragma warning restore CS1718
	}

	[Fact]
	public void ParseVersionString()
	{
		// Act & Assert
		EncryptionVersion.Parse("v1.0").ShouldBe(EncryptionVersion.Version10);
		EncryptionVersion.Parse("1.1").ShouldBe(EncryptionVersion.Version11);
		EncryptionVersion.Parse("V1.0").ShouldBe(EncryptionVersion.Version10);
	}

	[Fact]
	public void ReturnUnknown_WhenParsingInvalidString()
	{
		// Act & Assert
		EncryptionVersion.Parse("").ShouldBe(EncryptionVersion.Unknown);
		EncryptionVersion.Parse(null!).ShouldBe(EncryptionVersion.Unknown);
		EncryptionVersion.Parse("invalid").ShouldBe(EncryptionVersion.Unknown);
	}

	[Fact]
	public void FormatToString()
	{
		// Act & Assert
		EncryptionVersion.Version10.ToString().ShouldBe("v1.0");
		EncryptionVersion.Version11.ToString().ShouldBe("v1.1");
		EncryptionVersion.Unknown.ToString().ShouldBe("v0.0");
	}
}
