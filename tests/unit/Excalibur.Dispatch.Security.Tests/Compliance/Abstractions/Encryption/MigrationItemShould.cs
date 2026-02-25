// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationItemShould
{
    [Fact]
    public void CreateWithRequiredProperties()
    {
        var encData = new EncryptedData
        {
            Ciphertext = new byte[] { 1, 2, 3 },
            Iv = new byte[] { 4, 5, 6 },
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyId = "k-1",
            KeyVersion = 1,
            EncryptedAt = DateTimeOffset.UtcNow
        };

        var item = new EncryptionMigrationItem
        {
            ItemId = "item-1",
            EncryptedData = encData,
            SourceContext = EncryptionContext.Default
        };

        item.ItemId.ShouldBe("item-1");
        item.EncryptedData.ShouldBe(encData);
        item.SourceContext.ShouldBe(EncryptionContext.Default);
        item.Metadata.ShouldBeNull();
        item.Priority.ShouldBe(0);
    }

    [Fact]
    public void SupportOptionalMetadataAndPriority()
    {
        var encData = new EncryptedData
        {
            Ciphertext = new byte[] { 1 },
            Iv = new byte[] { 2 },
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            KeyId = "k-1",
            KeyVersion = 1,
            EncryptedAt = DateTimeOffset.UtcNow
        };

        var item = new EncryptionMigrationItem
        {
            ItemId = "item-2",
            EncryptedData = encData,
            SourceContext = EncryptionContext.Default,
            Metadata = new Dictionary<string, string> { ["source"] = "table1" },
            Priority = 5
        };

        item.Priority.ShouldBe(5);
        item.Metadata.ShouldContainKey("source");
    }
}
