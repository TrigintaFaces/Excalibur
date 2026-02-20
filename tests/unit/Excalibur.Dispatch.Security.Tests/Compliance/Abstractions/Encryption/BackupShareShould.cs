// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BackupShareShould
{
    private static BackupShare CreateShare(
        string keyId = "key-1",
        int keyVersion = 1,
        int shareIndex = 1,
        int totalShares = 5,
        int threshold = 3,
        string keyHash = "hash-1",
        DateTimeOffset? expiresAt = null,
        string? custodianId = null) =>
        new()
        {
            ShareId = $"share-{shareIndex}",
            KeyId = keyId,
            KeyVersion = keyVersion,
            ShareIndex = shareIndex,
            ShareData = new byte[] { (byte)shareIndex, 0x01, 0x02 },
            TotalShares = totalShares,
            Threshold = threshold,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            CustodianId = custodianId,
            KeyHash = keyHash
        };

    [Fact]
    public void ReportNotExpiredWhenNoExpiresAt()
    {
        var share = CreateShare();
        share.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void ReportExpiredWhenExpiresAtIsInPast()
    {
        var share = CreateShare(expiresAt: DateTimeOffset.UtcNow.AddHours(-1));
        share.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void ReportNotExpiredWhenExpiresAtIsInFuture()
    {
        var share = CreateShare(expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        share.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void DefaultFormatVersionToOne()
    {
        var share = CreateShare();
        share.FormatVersion.ShouldBe(1);
    }

    [Fact]
    public void CombineSharesSuccessfully()
    {
        // Arrange
        var shares = new[]
        {
            CreateShare(shareIndex: 1),
            CreateShare(shareIndex: 2),
            CreateShare(shareIndex: 3)
        };

        // Act
        var combined = BackupShare.Combine(shares);

        // Assert
        combined.ShareIndex.ShouldBe(0); // 0 indicates combined
        combined.KeyId.ShouldBe("key-1");
        combined.KeyVersion.ShouldBe(1);
        combined.Threshold.ShouldBe(3);
        combined.ShareData.ShouldNotBeNull();
        combined.ShareId.ShouldStartWith("combined-");
    }

    [Fact]
    public void ThrowWhenCombiningEmptyList()
    {
        Should.Throw<ArgumentException>(() => BackupShare.Combine(Array.Empty<BackupShare>()));
    }

    [Fact]
    public void ThrowWhenCombiningSharesFromDifferentKeys()
    {
        var shares = new[]
        {
            CreateShare(keyId: "key-1"),
            CreateShare(keyId: "key-2")
        };

        Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
    }

    [Fact]
    public void ThrowWhenCombiningSharesWithDifferentKeyVersions()
    {
        var shares = new[]
        {
            CreateShare(keyVersion: 1),
            CreateShare(keyVersion: 2)
        };

        Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
    }

    [Fact]
    public void ThrowWhenCombiningSharesWithDifferentThresholds()
    {
        var shares = new[]
        {
            CreateShare(threshold: 3),
            CreateShare(threshold: 4)
        };

        Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
    }

    [Fact]
    public void SupportCustodianId()
    {
        var share = CreateShare(custodianId: "custodian-alice");
        share.CustodianId.ShouldBe("custodian-alice");
    }
}
