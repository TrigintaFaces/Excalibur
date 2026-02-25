// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new ErasureOptions();

        options.DefaultGracePeriod.ShouldBe(TimeSpan.FromHours(72));
        options.MinimumGracePeriod.ShouldBe(TimeSpan.FromHours(1));
        options.MaximumGracePeriod.ShouldBe(TimeSpan.FromDays(30));
        options.EnableAutoDiscovery.ShouldBeTrue();
        options.RequireVerification.ShouldBeTrue();
        options.VerificationMethods.ShouldBe(VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem);
        options.NotifyOnCompletion.ShouldBeTrue();
        options.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 7));
        options.AllowImmediateErasure.ShouldBeFalse();
        options.SigningKeyId.ShouldBeNull();
        options.BatchSize.ShouldBe(100);
        options.MaxRetryAttempts.ShouldBe(3);
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void PassValidationWithDefaults()
    {
        var options = new ErasureOptions();
        Should.NotThrow(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenMinimumGracePeriodIsNegative()
    {
        var options = new ErasureOptions { MinimumGracePeriod = TimeSpan.FromHours(-1) };
        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenDefaultGracePeriodBelowMinimum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromMinutes(30),
            MinimumGracePeriod = TimeSpan.FromHours(1)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenDefaultGracePeriodExceedsMaximum()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromDays(31),
            MaximumGracePeriod = TimeSpan.FromDays(30)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenMaximumGracePeriodExceeds30Days()
    {
        var options = new ErasureOptions
        {
            DefaultGracePeriod = TimeSpan.FromDays(1),
            MaximumGracePeriod = TimeSpan.FromDays(31)
        };

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenBatchSizeIsLessThanOne()
    {
        var options = new ErasureOptions { BatchSize = 0 };
        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ThrowWhenMaxRetryAttemptsIsNegative()
    {
        var options = new ErasureOptions { MaxRetryAttempts = -1 };
        Should.Throw<InvalidOperationException>(() => options.Validate());
    }
}
