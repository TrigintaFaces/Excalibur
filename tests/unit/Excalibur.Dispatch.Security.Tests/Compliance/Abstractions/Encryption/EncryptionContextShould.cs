// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionContextShould
{
    [Fact]
    public void ReturnDefaultContextWithDefaultProperties()
    {
        // Act
        var context = EncryptionContext.Default;

        // Assert
        context.KeyId.ShouldBeNull();
        context.KeyVersion.ShouldBeNull();
        context.Algorithm.ShouldBeNull();
        context.TenantId.ShouldBeNull();
        context.Purpose.ShouldBeNull();
        context.AssociatedData.ShouldBeNull();
        context.Classification.ShouldBeNull();
        context.RequireFipsCompliance.ShouldBeFalse();
    }

    [Fact]
    public void CreateContextForTenant()
    {
        // Act
        var context = EncryptionContext.ForTenant("tenant-123");

        // Assert
        context.TenantId.ShouldBe("tenant-123");
        context.KeyId.ShouldBeNull();
    }

    [Fact]
    public void SupportInitializerSyntax()
    {
        // Arrange & Act
        var aad = new byte[] { 1, 2, 3 };
        var context = new EncryptionContext
        {
            KeyId = "key-1",
            KeyVersion = 2,
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            TenantId = "t1",
            Purpose = "pii-field",
            AssociatedData = aad,
            Classification = DataClassification.Restricted,
            RequireFipsCompliance = true
        };

        // Assert
        context.KeyId.ShouldBe("key-1");
        context.KeyVersion.ShouldBe(2);
        context.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
        context.TenantId.ShouldBe("t1");
        context.Purpose.ShouldBe("pii-field");
        context.AssociatedData.ShouldBe(aad);
        context.Classification.ShouldBe(DataClassification.Restricted);
        context.RequireFipsCompliance.ShouldBeTrue();
    }

    [Fact]
    public void SupportRecordEquality()
    {
        // Arrange
        var a = new EncryptionContext { KeyId = "k1", TenantId = "t1" };
        var b = new EncryptionContext { KeyId = "k1", TenantId = "t1" };

        // Assert
        a.ShouldBe(b);
    }
}
