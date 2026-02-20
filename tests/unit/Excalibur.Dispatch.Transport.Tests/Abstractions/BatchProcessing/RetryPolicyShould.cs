// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class RetryPolicyShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var policy = new RetryPolicy();

        policy.MaxRetries.ShouldBe(3);
        policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
        policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
        policy.BackoffMultiplier.ShouldBe(2.0);
        policy.UseExponentialBackoff.ShouldBeTrue();
        policy.UseJitter.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void AllowSettingMaxRetries(int maxRetries)
    {
        var policy = new RetryPolicy { MaxRetries = maxRetries };

        policy.MaxRetries.ShouldBe(maxRetries);
    }

    [Fact]
    public void AllowSettingInitialDelay()
    {
        var delay = TimeSpan.FromMilliseconds(500);
        var policy = new RetryPolicy { InitialDelay = delay };

        policy.InitialDelay.ShouldBe(delay);
    }

    [Fact]
    public void AllowSettingMaxDelay()
    {
        var delay = TimeSpan.FromMinutes(5);
        var policy = new RetryPolicy { MaxDelay = delay };

        policy.MaxDelay.ShouldBe(delay);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(1.0)]
    public void AllowSettingBackoffMultiplier(double multiplier)
    {
        var policy = new RetryPolicy { BackoffMultiplier = multiplier };

        policy.BackoffMultiplier.ShouldBe(multiplier);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingUseExponentialBackoff(bool useExponential)
    {
        var policy = new RetryPolicy { UseExponentialBackoff = useExponential };

        policy.UseExponentialBackoff.ShouldBe(useExponential);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingUseJitter(bool useJitter)
    {
        var policy = new RetryPolicy { UseJitter = useJitter };

        policy.UseJitter.ShouldBe(useJitter);
    }

    [Fact]
    public void AllowAggressiveRetryConfiguration()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 10,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 1.5,
            UseExponentialBackoff = true,
            UseJitter = true
        };

        policy.MaxRetries.ShouldBe(10);
        policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
        policy.BackoffMultiplier.ShouldBe(1.5);
        policy.UseExponentialBackoff.ShouldBeTrue();
        policy.UseJitter.ShouldBeTrue();
    }

    [Fact]
    public void AllowConservativeRetryConfiguration()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromSeconds(5),
            MaxDelay = TimeSpan.FromMinutes(2),
            BackoffMultiplier = 3.0,
            UseExponentialBackoff = true,
            UseJitter = false
        };

        policy.MaxRetries.ShouldBe(2);
        policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(5));
        policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(2));
        policy.BackoffMultiplier.ShouldBe(3.0);
        policy.UseExponentialBackoff.ShouldBeTrue();
        policy.UseJitter.ShouldBeFalse();
    }

    [Fact]
    public void AllowLinearRetryConfiguration()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(2),
            UseExponentialBackoff = false,
            UseJitter = false
        };

        policy.MaxRetries.ShouldBe(5);
        policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(2));
        policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(2));
        policy.UseExponentialBackoff.ShouldBeFalse();
        policy.UseJitter.ShouldBeFalse();
    }

    [Fact]
    public void AllowNoRetryConfiguration()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 0
        };

        policy.MaxRetries.ShouldBe(0);
    }
}
