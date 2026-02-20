// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitingOptionsCoverageShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Act
        var options = new RateLimitingOptions();

        // Assert
        options.Enabled.ShouldBeTrue();
        options.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
        options.DefaultLimits.ShouldNotBeNull();
        options.TenantLimits.ShouldNotBeNull();
        options.TenantLimits.Count.ShouldBe(0);
        options.TierLimits.ShouldNotBeNull();
        options.TierLimits.Count.ShouldBe(0);
        options.DefaultRetryAfterMilliseconds.ShouldBe(1000);
        options.CleanupIntervalMinutes.ShouldBe(5);
        options.InactivityTimeoutMinutes.ShouldBe(30);
    }

    [Fact]
    public void SetAllProperties()
    {
        // Act
        var options = new RateLimitingOptions
        {
            Enabled = false,
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            DefaultLimits = new RateLimits { PermitLimit = 50 },
            DefaultRetryAfterMilliseconds = 2000,
            CleanupIntervalMinutes = 10,
            InactivityTimeoutMinutes = 60,
        };
        options.TenantLimits["premium"] = new RateLimits { PermitLimit = 500 };
        options.TierLimits["enterprise"] = new RateLimits { PermitLimit = 1000 };

        // Assert
        options.Enabled.ShouldBeFalse();
        options.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
        options.DefaultLimits.PermitLimit.ShouldBe(50);
        options.DefaultRetryAfterMilliseconds.ShouldBe(2000);
        options.CleanupIntervalMinutes.ShouldBe(10);
        options.InactivityTimeoutMinutes.ShouldBe(60);
        options.TenantLimits["premium"].PermitLimit.ShouldBe(500);
        options.TierLimits["enterprise"].PermitLimit.ShouldBe(1000);
    }

    [Fact]
    public void RateLimitsHaveCorrectDefaults()
    {
        // Act
        var limits = new RateLimits();

        // Assert
        limits.TokenLimit.ShouldBe(100);
        limits.TokensPerPeriod.ShouldBe(20);
        limits.ReplenishmentPeriodSeconds.ShouldBe(1);
        limits.PermitLimit.ShouldBe(100);
        limits.WindowSeconds.ShouldBe(60);
        limits.SegmentsPerWindow.ShouldBe(4);
        limits.ConcurrencyLimit.ShouldBe(10);
        limits.QueueLimit.ShouldBe(10);
    }

    [Fact]
    public void RateLimitsSetAllProperties()
    {
        // Act
        var limits = new RateLimits
        {
            TokenLimit = 200,
            TokensPerPeriod = 50,
            ReplenishmentPeriodSeconds = 2,
            PermitLimit = 300,
            WindowSeconds = 120,
            SegmentsPerWindow = 8,
            ConcurrencyLimit = 20,
            QueueLimit = 5,
        };

        // Assert
        limits.TokenLimit.ShouldBe(200);
        limits.TokensPerPeriod.ShouldBe(50);
        limits.ReplenishmentPeriodSeconds.ShouldBe(2);
        limits.PermitLimit.ShouldBe(300);
        limits.WindowSeconds.ShouldBe(120);
        limits.SegmentsPerWindow.ShouldBe(8);
        limits.ConcurrencyLimit.ShouldBe(20);
        limits.QueueLimit.ShouldBe(5);
    }

    [Theory]
    [InlineData(RateLimitAlgorithm.Unknown, 0)]
    [InlineData(RateLimitAlgorithm.TokenBucket, 1)]
    [InlineData(RateLimitAlgorithm.SlidingWindow, 2)]
    [InlineData(RateLimitAlgorithm.FixedWindow, 3)]
    [InlineData(RateLimitAlgorithm.Concurrency, 4)]
    public void RateLimitAlgorithmEnumValues(RateLimitAlgorithm algorithm, int expectedValue)
    {
        ((int)algorithm).ShouldBe(expectedValue);
    }

    [Fact]
    public void RateLimitKeyPrefixesHaveExpectedValues()
    {
        RateLimitKeyPrefixes.Tenant.ShouldBe("tenant:");
        RateLimitKeyPrefixes.User.ShouldBe("user:");
        RateLimitKeyPrefixes.ApiKey.ShouldBe("api:");
        RateLimitKeyPrefixes.Ip.ShouldBe("ip:");
        RateLimitKeyPrefixes.MessageType.ShouldBe("type:");
        RateLimitKeyPrefixes.Tier.ShouldBe("tier:");
        RateLimitKeyPrefixes.Global.ShouldBe("global");
    }

    [Fact]
    public void RateLimitExceededResultProperties()
    {
        // Act
        var result = new RateLimitExceededResult
        {
            Succeeded = false,
            RetryAfterMilliseconds = 5000,
            RateLimitKey = "tenant:acme",
        };

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.RetryAfterMilliseconds.ShouldBe(5000);
        result.RateLimitKey.ShouldBe("tenant:acme");
    }
}
