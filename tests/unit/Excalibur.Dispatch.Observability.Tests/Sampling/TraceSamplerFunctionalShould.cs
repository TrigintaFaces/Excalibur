// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Sampling;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Functional tests for <see cref="TraceSampler"/> verifying sampling strategies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class TraceSamplerFunctionalShould
{
	[Fact]
	public void AlwaysOn_AlwaysSamples()
	{
		var sampler = CreateSampler(SamplingStrategy.AlwaysOn);

		for (var i = 0; i < 100; i++)
		{
			sampler.ShouldSample(default, "test").ShouldBeTrue();
		}
	}

	[Fact]
	public void AlwaysOff_NeverSamples()
	{
		var sampler = CreateSampler(SamplingStrategy.AlwaysOff);

		for (var i = 0; i < 100; i++)
		{
			sampler.ShouldSample(default, "test").ShouldBeFalse();
		}
	}

	[Fact]
	public void RatioBased_WithFullRatio_AlwaysSamples()
	{
		var sampler = CreateSampler(SamplingStrategy.RatioBased, ratio: 1.0);

		for (var i = 0; i < 100; i++)
		{
			sampler.ShouldSample(default, "test").ShouldBeTrue();
		}
	}

	[Fact]
	public void RatioBased_WithZeroRatio_NeverSamples()
	{
		var sampler = CreateSampler(SamplingStrategy.RatioBased, ratio: 0.0);

		for (var i = 0; i < 100; i++)
		{
			sampler.ShouldSample(default, "test").ShouldBeFalse();
		}
	}

	[Fact]
	public void RatioBased_WithHalfRatio_SamplesSomeButNotAll()
	{
		var sampler = CreateSampler(SamplingStrategy.RatioBased, ratio: 0.5);

		var sampledCount = 0;
		const int iterations = 10000;
		for (var i = 0; i < iterations; i++)
		{
			if (sampler.ShouldSample(default, "test"))
			{
				sampledCount++;
			}
		}

		// With 10000 iterations and 50% ratio, expect roughly 5000 +/- reasonable margin
		sampledCount.ShouldBeGreaterThan(iterations / 4);
		sampledCount.ShouldBeLessThan(iterations * 3 / 4);
	}

	[Fact]
	public void ParentBased_InheritsParentRecordedFlag()
	{
		var sampler = CreateSampler(SamplingStrategy.ParentBased, ratio: 0.0); // ratio 0 as fallback
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

		sampler.ShouldSample(parentContext, "test").ShouldBeTrue();
	}

	[Fact]
	public void ParentBased_InheritsParentNotRecordedFlag()
	{
		var sampler = CreateSampler(SamplingStrategy.ParentBased, ratio: 1.0); // ratio 1 as fallback
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);

		sampler.ShouldSample(parentContext, "test").ShouldBeFalse();
	}

	[Fact]
	public void ParentBased_FallsBackToRatio_ForRootSpan()
	{
		var sampler = CreateSampler(SamplingStrategy.ParentBased, ratio: 1.0);

		// Default context (no parent) should fall back to ratio-based
		sampler.ShouldSample(default, "test").ShouldBeTrue();
	}

	[Fact]
	public void ParentBased_FallsBackToRatio_ForRootSpan_WithZeroRatio()
	{
		var sampler = CreateSampler(SamplingStrategy.ParentBased, ratio: 0.0);

		sampler.ShouldSample(default, "test").ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSampler(null!));
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
