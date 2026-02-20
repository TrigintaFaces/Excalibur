// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Sampling;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSampler"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class TraceSamplerShould
{
	[Fact]
	public void AlwaysSample_WhenStrategyIsAlwaysOn()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.AlwaysOn);

		// Act & Assert
		sampler.ShouldSample(default, "test").ShouldBeTrue();
	}

	[Fact]
	public void NeverSample_WhenStrategyIsAlwaysOff()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.AlwaysOff);

		// Act & Assert
		sampler.ShouldSample(default, "test").ShouldBeFalse();
	}

	[Fact]
	public void AlwaysSample_WhenRatioIsOne()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.RatioBased, 1.0);

		// Act — run multiple times to verify deterministic behavior
		var results = Enumerable.Range(0, 100).Select(_ => sampler.ShouldSample(default, "test")).ToList();

		// Assert
		results.ShouldAllBe(r => r);
	}

	[Fact]
	public void NeverSample_WhenRatioIsZero()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.RatioBased, 0.0);

		// Act
		var results = Enumerable.Range(0, 100).Select(_ => sampler.ShouldSample(default, "test")).ToList();

		// Assert
		results.ShouldAllBe(r => !r);
	}

	[Fact]
	public void SampleApproximateRatio_WhenRatioIsHalf()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.RatioBased, 0.5);

		// Act — run enough times to get a statistically meaningful result
		var sampledCount = Enumerable.Range(0, 10000)
			.Count(_ => sampler.ShouldSample(default, "test"));

		// Assert — should be roughly 50% (with generous tolerance)
		sampledCount.ShouldBeGreaterThan(3000);
		sampledCount.ShouldBeLessThan(7000);
	}

	[Fact]
	public void InheritParentDecision_WhenParentBased_AndParentIsRecorded()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.ParentBased);
		var parentContext = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.Recorded);

		// Act
		var result = sampler.ShouldSample(parentContext, "test");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void RejectParentDecision_WhenParentBased_AndParentIsNotRecorded()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.ParentBased);
		var parentContext = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.None);

		// Act
		var result = sampler.ShouldSample(parentContext, "test");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void FallBackToRatio_WhenParentBased_AndNoParent()
	{
		// Arrange
		var sampler = CreateSampler(SamplingStrategy.ParentBased, 1.0);

		// Act — default context means no parent
		var result = sampler.ShouldSample(default, "test");

		// Assert — should fall back to ratio-based (1.0 = always sample)
		result.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSampler(null!));
	}

	[Fact]
	public void ImplementITraceSampler()
	{
		var sampler = CreateSampler(SamplingStrategy.AlwaysOn);
		sampler.ShouldBeAssignableTo<ITraceSampler>();
	}

	private static TraceSampler CreateSampler(SamplingStrategy strategy, double ratio = 1.0)
	{
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = strategy,
			SamplingRatio = ratio,
		});
		return new TraceSampler(options);
	}
}
