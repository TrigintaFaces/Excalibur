// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Unit tests for <see cref="LazyReEncryptionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Encryption")]
public sealed class LazyReEncryptionOptionsShould
{
    [Fact]
    public void DefaultEnabledToTrue()
    {
        var options = new LazyReEncryptionOptions();
        options.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void DefaultContinueOnFailureToTrue()
    {
        var options = new LazyReEncryptionOptions();
        options.ContinueOnFailure.ShouldBeTrue();
    }

    [Fact]
    public void DefaultTargetAlgorithmToNull()
    {
        var options = new LazyReEncryptionOptions();
        options.TargetAlgorithm.ShouldBeNull();
    }

    [Fact]
    public void DefaultTargetKeyIdToNull()
    {
        var options = new LazyReEncryptionOptions();
        options.TargetKeyId.ShouldBeNull();
    }

    [Fact]
    public void DefaultOnReEncryptedToNull()
    {
        var options = new LazyReEncryptionOptions();
        options.OnReEncrypted.ShouldBeNull();
    }

    [Fact]
    public void DefaultMigrationPolicyToNotNull()
    {
        var options = new LazyReEncryptionOptions();
        options.MigrationPolicy.ShouldNotBeNull();
    }

    [Fact]
    public void AllowSettingTargetAlgorithm()
    {
        var options = new LazyReEncryptionOptions
        {
            TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm,
        };

        options.TargetAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
    }

    [Fact]
    public void AllowSettingTargetKeyId()
    {
        var options = new LazyReEncryptionOptions
        {
            TargetKeyId = "my-target-key",
        };

        options.TargetKeyId.ShouldBe("my-target-key");
    }

    [Fact]
    public void AllowSettingOnReEncryptedCallback()
    {
        // Arrange
        Func<EncryptedData, EncryptedData, CancellationToken, Task> callback = (_, _, _) => Task.CompletedTask;

        // Act
        var options = new LazyReEncryptionOptions
        {
            OnReEncrypted = callback,
        };

        // Assert
        options.OnReEncrypted.ShouldNotBeNull();
        options.OnReEncrypted.ShouldBe(callback);
    }

    [Fact]
    public void AllowSettingMigrationPolicy()
    {
        var policy = new MigrationPolicy
        {
            DeprecatedKeyIds = new HashSet<string> { "old-key" },
        };

        var options = new LazyReEncryptionOptions
        {
            MigrationPolicy = policy,
        };

        options.MigrationPolicy.ShouldBe(policy);
    }

    [Fact]
    public void BeSealed()
    {
        typeof(LazyReEncryptionOptions).IsSealed.ShouldBeTrue();
    }
}
