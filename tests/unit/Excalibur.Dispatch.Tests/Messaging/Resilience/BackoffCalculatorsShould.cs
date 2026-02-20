// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
public sealed class BackoffCalculatorsShould
{
    // --- FixedBackoffCalculator ---

    [Fact]
    public void FixedBackoff_ReturnsSameDelayForEveryAttempt()
    {
        var sut = new FixedBackoffCalculator(TimeSpan.FromSeconds(2));

        sut.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(2));
        sut.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(2));
        sut.CalculateDelay(5).ShouldBe(TimeSpan.FromSeconds(2));
    }

    // --- LinearBackoffCalculator ---

    [Fact]
    public void LinearBackoff_IncreasesLinearly()
    {
        var sut = new LinearBackoffCalculator(TimeSpan.FromSeconds(1));

        var delay1 = sut.CalculateDelay(1);
        var delay2 = sut.CalculateDelay(2);
        var delay3 = sut.CalculateDelay(3);

        delay2.ShouldBeGreaterThan(delay1);
        delay3.ShouldBeGreaterThan(delay2);
    }

    // --- ExponentialBackoffCalculator ---

    [Fact]
    public void ExponentialBackoff_IncreasesExponentially()
    {
        var sut = new ExponentialBackoffCalculator(
            TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30), enableJitter: false);

        var delay1 = sut.CalculateDelay(1);
        var delay2 = sut.CalculateDelay(2);
        var delay3 = sut.CalculateDelay(3);

        delay2.ShouldBeGreaterThan(delay1);
        delay3.ShouldBeGreaterThan(delay2);

        // Exponential growth: delay3 - delay2 should be greater than delay2 - delay1
        (delay3 - delay2).ShouldBeGreaterThan(delay2 - delay1);
    }

    // --- BackoffCalculatorFactory ---

    [Fact]
    public void Factory_CreatesFixedCalculator()
    {
        var options = new RetryPolicyOptions { BaseDelay = TimeSpan.FromSeconds(1) };
        var calculator = BackoffCalculatorFactory.Create(BackoffStrategy.Fixed, options);

        calculator.ShouldBeOfType<FixedBackoffCalculator>();
    }

    [Fact]
    public void Factory_CreatesLinearCalculator()
    {
        var options = new RetryPolicyOptions { BaseDelay = TimeSpan.FromSeconds(1) };
        var calculator = BackoffCalculatorFactory.Create(BackoffStrategy.Linear, options);

        calculator.ShouldBeOfType<LinearBackoffCalculator>();
    }

    [Fact]
    public void Factory_CreatesExponentialCalculator()
    {
        var options = new RetryPolicyOptions { BaseDelay = TimeSpan.FromSeconds(1) };
        var calculator = BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, options);

        calculator.ShouldBeOfType<ExponentialBackoffCalculator>();
    }

    [Fact]
    public void Factory_CreatesExponentialWithJitterCalculator()
    {
        var options = new RetryPolicyOptions { BaseDelay = TimeSpan.FromSeconds(1) };
        var calculator = BackoffCalculatorFactory.Create(BackoffStrategy.ExponentialWithJitter, options);

        // Should return an exponential calculator (with jitter added externally)
        calculator.ShouldNotBeNull();
    }
}
