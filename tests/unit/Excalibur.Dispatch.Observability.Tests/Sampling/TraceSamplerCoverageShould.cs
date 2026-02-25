// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Sampling;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TraceSamplerCoverageShould
{
    [Fact]
    public void SampleAllWhenAlwaysOn()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions { Strategy = SamplingStrategy.AlwaysOn });
        var sut = new TraceSampler(options);

        // Act & Assert
        sut.ShouldSample(default, "test").ShouldBeTrue();
    }

    [Fact]
    public void SampleNoneWhenAlwaysOff()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions { Strategy = SamplingStrategy.AlwaysOff });
        var sut = new TraceSampler(options);

        // Act & Assert
        sut.ShouldSample(default, "test").ShouldBeFalse();
    }

    [Fact]
    public void SampleAllWhenRatioIsOne()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions
        {
            Strategy = SamplingStrategy.RatioBased,
            SamplingRatio = 1.0,
        });
        var sut = new TraceSampler(options);

        // Act & Assert - should always return true with ratio 1.0
        for (var i = 0; i < 100; i++)
        {
            sut.ShouldSample(default, "test").ShouldBeTrue();
        }
    }

    [Fact]
    public void SampleNoneWhenRatioIsZero()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions
        {
            Strategy = SamplingStrategy.RatioBased,
            SamplingRatio = 0.0,
        });
        var sut = new TraceSampler(options);

        // Act & Assert - should always return false with ratio 0.0
        for (var i = 0; i < 100; i++)
        {
            sut.ShouldSample(default, "test").ShouldBeFalse();
        }
    }

    [Fact]
    public void SampleProportionallyWithMidRatio()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions
        {
            Strategy = SamplingStrategy.RatioBased,
            SamplingRatio = 0.5,
        });
        var sut = new TraceSampler(options);

        // Act
        var sampledCount = 0;
        var totalIterations = 1000;
        for (var i = 0; i < totalIterations; i++)
        {
            if (sut.ShouldSample(default, "test"))
            {
                sampledCount++;
            }
        }

        // Assert - should be roughly 50% (+/- reasonable margin)
        sampledCount.ShouldBeGreaterThan(200);
        sampledCount.ShouldBeLessThan(800);
    }

    [Fact]
    public void ParentBasedInheritFromParentWhenRecorded()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions { Strategy = SamplingStrategy.ParentBased });
        var sut = new TraceSampler(options);

        // Create parent context with Recorded flag
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        // Act
        var shouldSample = sut.ShouldSample(parentContext, "test");

        // Assert - should inherit parent's decision
        shouldSample.ShouldBeTrue();
    }

    [Fact]
    public void ParentBasedInheritFromParentWhenNotRecorded()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions { Strategy = SamplingStrategy.ParentBased });
        var sut = new TraceSampler(options);

        // Create parent context without Recorded flag
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None);

        // Act
        var shouldSample = sut.ShouldSample(parentContext, "test");

        // Assert - should inherit parent's decision (not recorded)
        shouldSample.ShouldBeFalse();
    }

    [Fact]
    public void ParentBasedFallbackToRatioForRootSpan()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions
        {
            Strategy = SamplingStrategy.ParentBased,
            SamplingRatio = 1.0, // Always sample as fallback
        });
        var sut = new TraceSampler(options);

        // Act - use default context (no parent)
        var shouldSample = sut.ShouldSample(default, "test");

        // Assert - should fall back to ratio
        shouldSample.ShouldBeTrue();
    }

    [Fact]
    public void ThrowForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TraceSampler(null!));
    }

    [Fact]
    public void HandleUnknownStrategyAsAlwaysOn()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new TraceSamplerOptions
        {
            Strategy = (SamplingStrategy)99,
        });
        var sut = new TraceSampler(options);

        // Act
        var result = sut.ShouldSample(default, "test");

        // Assert - default case returns true
        result.ShouldBeTrue();
    }
}
