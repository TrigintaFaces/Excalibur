// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EscrowModelsShould
{
    [Fact]
    public void SetEscrowOptionsDefaults()
    {
        var options = new EscrowOptions();

        options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
        options.ExpiresIn.ShouldBeNull();
        options.Purpose.ShouldBeNull();
        options.TenantId.ShouldBeNull();
        options.Metadata.ShouldBeNull();
        options.AllowOverwrite.ShouldBeFalse();
    }

    [Fact]
    public void SetEscrowOptionsProperties()
    {
        var options = new EscrowOptions
        {
            Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
            ExpiresIn = TimeSpan.FromDays(30),
            Purpose = "backup",
            TenantId = "t1",
            Metadata = new Dictionary<string, string> { ["env"] = "prod" },
            AllowOverwrite = true
        };

        options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256CbcHmac);
        options.ExpiresIn.ShouldBe(TimeSpan.FromDays(30));
        options.Purpose.ShouldBe("backup");
        options.TenantId.ShouldBe("t1");
        options.AllowOverwrite.ShouldBeTrue();
    }

    [Fact]
    public void CreateEscrowReceipt()
    {
        var receipt = new EscrowReceipt
        {
            KeyId = "key-1",
            EscrowId = "escrow-1",
            EscrowedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
            KeyHash = "abc123",
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            MasterKeyVersion = 3,
            Metadata = new Dictionary<string, string> { ["tag"] = "value" }
        };

        receipt.KeyId.ShouldBe("key-1");
        receipt.EscrowId.ShouldBe("escrow-1");
        receipt.KeyHash.ShouldBe("abc123");
        receipt.MasterKeyVersion.ShouldBe(3);
        receipt.Metadata.ShouldNotBeNull();
    }
}
