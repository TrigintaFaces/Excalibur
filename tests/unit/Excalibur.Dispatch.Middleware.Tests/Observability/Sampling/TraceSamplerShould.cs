// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Sampling;

using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSampler"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceSamplerShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSampler(null!));
	}

	[Fact]
	public void ShouldSample_ReturnsTrue_WhenStrategyIsAlwaysOn()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.AlwaysOn,
		});
		var sampler = new TraceSampler(options);

		// Act
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldSample_ReturnsFalse_WhenStrategyIsAlwaysOff()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.AlwaysOff,
		});
		var sampler = new TraceSampler(options);

		// Act
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldSample_ReturnsTrue_WhenRatioBasedWithFullSampling()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.RatioBased,
			SamplingRatio = 1.0,
		});
		var sampler = new TraceSampler(options);

		// Act
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldSample_ReturnsFalse_WhenRatioBasedWithZeroSampling()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.RatioBased,
			SamplingRatio = 0.0,
		});
		var sampler = new TraceSampler(options);

		// Act
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldSample_ParentBased_InheritsRecordedFlag_WhenParentIsRecorded()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.ParentBased,
		});
		var sampler = new TraceSampler(options);

		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);

		// Act
		var result = sampler.ShouldSample(parentContext, "test.operation");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldSample_ParentBased_InheritsNotRecorded_WhenParentIsNotRecorded()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.ParentBased,
		});
		var sampler = new TraceSampler(options);

		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);

		// Act
		var result = sampler.ShouldSample(parentContext, "test.operation");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldSample_ParentBased_FallsBackToRatio_WhenNoParent()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.ParentBased,
			SamplingRatio = 1.0,
		});
		var sampler = new TraceSampler(options);

		// Act - no parent context (default)
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldSample_ParentBased_ReturnsFalse_WhenNoParentAndRatioIsZero()
	{
		// Arrange
		var options = MsOptions.Create(new TraceSamplerOptions
		{
			Strategy = SamplingStrategy.ParentBased,
			SamplingRatio = 0.0,
		});
		var sampler = new TraceSampler(options);

		// Act
		var result = sampler.ShouldSample(default, "test.operation");

		// Assert
		result.ShouldBeFalse();
	}
}
