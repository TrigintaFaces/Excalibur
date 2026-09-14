using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Versioning;

using ConfigValidationSeverity = Excalibur.Dispatch.Validation.ValidationSeverity;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Enums;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MoreEnumsShould
{
	[Fact]
	public void CircuitState_HaveExpectedValues()
	{
		CircuitState.Closed.ShouldBe((CircuitState)0);
		CircuitState.Open.ShouldBe((CircuitState)1);
		CircuitState.HalfOpen.ShouldBe((CircuitState)2);
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
		ConfigValidationSeverity.Error!.ShouldBe((ConfigValidationSeverity)2);
	}

	// --- Middleware enums ---

	[Fact]
	public void MiddlewareRateLimitAlgorithm_HaveExpectedValues()
	{
		MiddlewareRateLimitAlgorithm.TokenBucket.ShouldBe((MiddlewareRateLimitAlgorithm)0);
		MiddlewareRateLimitAlgorithm.SlidingWindow.ShouldBe((MiddlewareRateLimitAlgorithm)1);
		MiddlewareRateLimitAlgorithm.FixedWindow.ShouldBe((MiddlewareRateLimitAlgorithm)2);
		MiddlewareRateLimitAlgorithm.Concurrency.ShouldBe((MiddlewareRateLimitAlgorithm)3);
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
