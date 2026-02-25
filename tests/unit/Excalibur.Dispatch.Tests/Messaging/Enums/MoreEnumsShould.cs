using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Middleware;

using ConfigValidationSeverity = Excalibur.Dispatch.Configuration.ValidationSeverity;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Enums;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MoreEnumsShould
{
	// --- CloudNative enums ---

	[Fact]
	public void AdaptationImpact_HaveExpectedValues()
	{
		AdaptationImpact.Minor.ShouldBe((AdaptationImpact)0);
		AdaptationImpact.Moderate.ShouldBe((AdaptationImpact)1);
		AdaptationImpact.Major.ShouldBe((AdaptationImpact)2);
	}

	[Fact]
	public void AdaptationState_HaveExpectedValues()
	{
		AdaptationState.Stable.ShouldBe((AdaptationState)0);
		AdaptationState.Adapting.ShouldBe((AdaptationState)1);
		AdaptationState.Monitoring.ShouldBe((AdaptationState)2);
	}

	[Fact]
	public void PatternHealthStatus_HaveExpectedValues()
	{
		PatternHealthStatus.Unknown.ShouldBe((PatternHealthStatus)0);
		PatternHealthStatus.Healthy.ShouldBe((PatternHealthStatus)1);
		PatternHealthStatus.Degraded.ShouldBe((PatternHealthStatus)2);
		PatternHealthStatus.Unhealthy.ShouldBe((PatternHealthStatus)3);
		PatternHealthStatus.Critical.ShouldBe((PatternHealthStatus)4);
	}

	[Fact]
	public void ResilienceState_HaveExpectedValues()
	{
		ResilienceState.Closed.ShouldBe((ResilienceState)0);
		ResilienceState.Open.ShouldBe((ResilienceState)1);
		ResilienceState.HalfOpen.ShouldBe((ResilienceState)2);
	}

	// --- Configuration enums ---

	[Fact]
	public void PipelineComplexity_HaveExpectedValues()
	{
		PipelineComplexity.Standard.ShouldBe((PipelineComplexity)0);
		PipelineComplexity.Reduced.ShouldBe((PipelineComplexity)1);
		PipelineComplexity.Minimal.ShouldBe((PipelineComplexity)2);
		PipelineComplexity.Direct.ShouldBe((PipelineComplexity)3);
	}

	[Fact]
	public void ConfigurationValidationSeverity_HaveExpectedValues()
	{
		ConfigValidationSeverity.Info.ShouldBe((ConfigValidationSeverity)0);
		ConfigValidationSeverity.Warning.ShouldBe((ConfigValidationSeverity)1);
		ConfigValidationSeverity.Error.ShouldBe((ConfigValidationSeverity)2);
	}

	// --- Middleware enums ---

	[Fact]
	public void RateLimitAlgorithm_HaveExpectedValues()
	{
		RateLimitAlgorithm.TokenBucket.ShouldBe((RateLimitAlgorithm)0);
		RateLimitAlgorithm.SlidingWindow.ShouldBe((RateLimitAlgorithm)1);
		RateLimitAlgorithm.FixedWindow.ShouldBe((RateLimitAlgorithm)2);
		RateLimitAlgorithm.Concurrency.ShouldBe((RateLimitAlgorithm)3);
	}

	[Fact]
	public void VersionCompatibilityStatus_HaveExpectedValues()
	{
		VersionCompatibilityStatus.Compatible.ShouldBe((VersionCompatibilityStatus)0);
		VersionCompatibilityStatus.Deprecated.ShouldBe((VersionCompatibilityStatus)1);
		VersionCompatibilityStatus.Incompatible.ShouldBe((VersionCompatibilityStatus)2);
		VersionCompatibilityStatus.Unknown.ShouldBe((VersionCompatibilityStatus)3);
	}
}
