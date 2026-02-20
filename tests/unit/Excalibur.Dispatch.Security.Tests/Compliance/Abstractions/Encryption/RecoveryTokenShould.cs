// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RecoveryTokenShould
{
    private static RecoveryToken CreateToken(
        string keyId = "key-1",
        string escrowId = "escrow-1",
        int shareIndex = 1,
        int totalShares = 5,
        int threshold = 3,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            TokenId = $"token-{shareIndex}",
            KeyId = keyId,
            EscrowId = escrowId,
            ShareIndex = shareIndex,
            ShareData = new byte[] { (byte)shareIndex, 0xAA, 0xBB },
            TotalShares = totalShares,
            Threshold = threshold,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(30)
        };

    [Fact]
    public void ReportNotExpiredWhenExpiresAtIsInFuture()
    {
        var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddDays(1));
        token.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void ReportExpiredWhenExpiresAtIsInPast()
    {
        var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        token.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void CombineTokensSuccessfully()
    {
        // Arrange
        var tokens = new[]
        {
            CreateToken(shareIndex: 1),
            CreateToken(shareIndex: 2),
            CreateToken(shareIndex: 3)
        };

        // Act
        var combined = RecoveryToken.Combine(tokens);

        // Assert
        combined.ShareIndex.ShouldBe(0); // 0 = combined
        combined.KeyId.ShouldBe("key-1");
        combined.EscrowId.ShouldBe("escrow-1");
        combined.TokenId.ShouldStartWith("combined-");
        combined.ShareData.ShouldNotBeNull();
        combined.ShareData.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ThrowWhenCombiningNull()
    {
        Should.Throw<ArgumentNullException>(() => RecoveryToken.Combine(null!));
    }

    [Fact]
    public void ThrowWhenCombiningEmptyList()
    {
        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(Array.Empty<RecoveryToken>()));
    }

    [Fact]
    public void ThrowWhenTokensAreFromDifferentEscrows()
    {
        var tokens = new[]
        {
            CreateToken(escrowId: "escrow-1"),
            CreateToken(escrowId: "escrow-2")
        };

        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens));
    }

    [Fact]
    public void ThrowWhenTokensAreFromDifferentKeys()
    {
        var tokens = new[]
        {
            CreateToken(keyId: "key-1"),
            CreateToken(keyId: "key-2")
        };

        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens));
    }

    [Fact]
    public void ThrowWhenThresholdNotMet()
    {
        // threshold = 3 but only 2 tokens
        var tokens = new[]
        {
            CreateToken(shareIndex: 1, threshold: 3),
            CreateToken(shareIndex: 2, threshold: 3)
        };

        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens));
    }

    [Fact]
    public void ThrowWhenDuplicateShareIndicesProvided()
    {
        var tokens = new[]
        {
            CreateToken(shareIndex: 1, threshold: 2),
            CreateToken(shareIndex: 1, threshold: 2),
            CreateToken(shareIndex: 2, threshold: 2)
        };

        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens));
    }

    [Fact]
    public void ThrowWhenExpiredTokensProvided()
    {
        var tokens = new[]
        {
            CreateToken(shareIndex: 1, threshold: 2, expiresAt: DateTimeOffset.UtcNow.AddDays(-1)),
            CreateToken(shareIndex: 2, threshold: 2),
            CreateToken(shareIndex: 3, threshold: 2)
        };

        Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens));
    }

    [Fact]
    public void SetCombinedExpiresAtToMinimumOfAllTokens()
    {
        var earlyExpiry = DateTimeOffset.UtcNow.AddDays(1);
        var lateExpiry = DateTimeOffset.UtcNow.AddDays(30);

        var tokens = new[]
        {
            CreateToken(shareIndex: 1, threshold: 2, expiresAt: earlyExpiry),
            CreateToken(shareIndex: 2, threshold: 2, expiresAt: lateExpiry),
            CreateToken(shareIndex: 3, threshold: 2, expiresAt: lateExpiry)
        };

        var combined = RecoveryToken.Combine(tokens);
        combined.ExpiresAt.ShouldBe(earlyExpiry);
    }

    [Fact]
    public void SupportCustodianId()
    {
        var token = CreateToken();
        var tokenWithCustodian = token with { CustodianId = "alice" };
        tokenWithCustodian.CustodianId.ShouldBe("alice");
    }
}
