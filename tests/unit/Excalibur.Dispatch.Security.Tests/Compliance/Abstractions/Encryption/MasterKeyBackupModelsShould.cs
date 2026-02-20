// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MasterKeyBackupModelsShould
{
    [Fact]
    public void ReportBackupNotExpiredWhenNoExpiresAt()
    {
        var backup = new MasterKeyBackup
        {
            BackupId = "b-1",
            KeyId = "k-1",
            KeyVersion = 1,
            EncryptedKeyMaterial = new byte[] { 1, 2, 3 },
            WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow
        };

        backup.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void ReportBackupExpiredWhenPast()
    {
        var backup = new MasterKeyBackup
        {
            BackupId = "b-1",
            KeyId = "k-1",
            KeyVersion = 1,
            EncryptedKeyMaterial = new byte[] { 1, 2, 3 },
            WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        backup.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void DefaultFormatVersionToOne()
    {
        var backup = new MasterKeyBackup
        {
            BackupId = "b-1",
            KeyId = "k-1",
            KeyVersion = 1,
            EncryptedKeyMaterial = new byte[] { 1 },
            WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow
        };

        backup.FormatVersion.ShouldBe(1);
    }

    [Fact]
    public void SetExportOptionsDefaults()
    {
        var options = new MasterKeyExportOptions();

        options.WrappingKeyId.ShouldBeNull();
        options.WrappingAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
        options.ExpiresIn.ShouldBe(TimeSpan.FromDays(90));
        options.Metadata.ShouldBeNull();
        options.Reason.ShouldBeNull();
    }

    [Fact]
    public void SetImportOptionsDefaults()
    {
        var options = new MasterKeyImportOptions();

        options.AllowOverwrite.ShouldBeFalse();
        options.ActivateImmediately.ShouldBeTrue();
        options.NewKeyId.ShouldBeNull();
        options.Reason.ShouldBeNull();
        options.VerifyKeyHash.ShouldBeTrue();
    }

    [Fact]
    public void SetBackupShareOptionsDefaults()
    {
        var options = new BackupShareOptions();

        options.ExpiresIn.ShouldBe(TimeSpan.FromDays(365));
        options.CustodianIds.ShouldBeNull();
        options.Reason.ShouldBeNull();
    }

    [Fact]
    public void CreateImportResult()
    {
        var result = new MasterKeyImportResult
        {
            Success = true,
            KeyId = "k-1",
            KeyVersion = 3,
            ImportedAt = DateTimeOffset.UtcNow,
            WasOverwritten = true,
            Warnings = new[] { "Warning 1" }
        };

        result.Success.ShouldBeTrue();
        result.WasOverwritten.ShouldBeTrue();
        result.Warnings.ShouldNotBeNull();
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateBackupVerificationResult()
    {
        var result = new BackupVerificationResult
        {
            IsValid = true,
            KeyId = "k-1",
            KeyVersion = 1,
            FormatSupported = true,
            IntegrityCheckPassed = true
        };

        result.IsValid.ShouldBeTrue();
        result.FormatSupported.ShouldBeTrue();
    }

    [Fact]
    public void CreateBackupStatus()
    {
        var status = new MasterKeyBackupStatus
        {
            KeyId = "k-1",
            CurrentVersion = 5,
            HasBackup = true,
            LastBackupAt = DateTimeOffset.UtcNow,
            ActiveShareCount = 5,
            ShareThreshold = 3,
            IsAtRisk = false
        };

        status.HasBackup.ShouldBeTrue();
        status.ActiveShareCount.ShouldBe(5);
    }
}
