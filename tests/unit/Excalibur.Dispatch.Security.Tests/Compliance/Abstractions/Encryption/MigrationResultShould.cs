// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationResultShould
{
    [Fact]
    public void CreateSucceededResult()
    {
        // Arrange
        var data = new EncryptedData
        {
            Ciphertext = new byte[] { 1, 2, 3 },
            Iv = new byte[] { 4, 5, 6 },
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyId = "target-key",
            KeyVersion = 2,
            EncryptedAt = DateTimeOffset.UtcNow
        };
        var duration = TimeSpan.FromSeconds(1.5);

        // Act
        var result = EncryptionMigrationResult.Succeeded(data, duration, "src-key", "target-key");

        // Assert
        result.Success.ShouldBeTrue();
        result.MigratedData.ShouldBe(data);
        result.Duration.ShouldBe(duration);
        result.SourceKeyId.ShouldBe("src-key");
        result.TargetKeyId.ShouldBe("target-key");
        result.ErrorMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public void CreateFailedResult()
    {
        // Arrange
        var ex = new InvalidOperationException("oops");

        // Act
        var result = EncryptionMigrationResult.Failed("Something failed", ex, TimeSpan.FromMilliseconds(50));

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Something failed");
        result.Exception.ShouldBe(ex);
        result.MigratedData.ShouldBeNull();
    }

    [Fact]
    public void CreateFailedResultWithDefaultDuration()
    {
        var result = EncryptionMigrationResult.Failed("error");

        result.Duration.ShouldBe(TimeSpan.Zero);
        result.Exception.ShouldBeNull();
    }
}
