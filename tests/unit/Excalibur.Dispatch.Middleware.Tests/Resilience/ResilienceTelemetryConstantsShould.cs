// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="ResilienceTelemetryConstants"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class ResilienceTelemetryConstantsShould : UnitTestBase
{
	[Fact]
	public void MeterName_HasExpectedValue()
	{
		ResilienceTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Resilience");
	}

	[Fact]
	public void ActivitySourceName_HasExpectedValue()
	{
		ResilienceTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Resilience");
	}

	[Fact]
	public void Version_IsNotNullOrEmpty()
	{
		ResilienceTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}

	#region Instruments

	[Fact]
	public void Instruments_RetryAttempts_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Instruments.RetryAttempts
			.ShouldBe("dispatch.resilience.retry.attempts");
	}

	[Fact]
	public void Instruments_CircuitBreakerTransitions_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Instruments.CircuitBreakerTransitions
			.ShouldBe("dispatch.resilience.circuit_breaker.transitions");
	}

	[Fact]
	public void Instruments_OperationDuration_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Instruments.OperationDuration
			.ShouldBe("dispatch.resilience.operation.duration");
	}

	[Fact]
	public void Instruments_Timeouts_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Instruments.Timeouts
			.ShouldBe("dispatch.resilience.timeouts");
	}

	[Fact]
	public void Instruments_OperationsExecuted_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Instruments.OperationsExecuted
			.ShouldBe("dispatch.resilience.operations.executed");
	}

	#endregion

	#region Tags

	[Fact]
	public void Tags_PipelineName_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Tags.PipelineName.ShouldBe("resilience.pipeline.name");
	}

	[Fact]
	public void Tags_StrategyType_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Tags.StrategyType.ShouldBe("resilience.strategy.type");
	}

	[Fact]
	public void Tags_Outcome_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Tags.Outcome.ShouldBe("resilience.outcome");
	}

	[Fact]
	public void Tags_CircuitState_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Tags.CircuitState.ShouldBe("resilience.circuit_breaker.state");
	}

	[Fact]
	public void Tags_RetryAttempt_HasExpectedValue()
	{
		ResilienceTelemetryConstants.Tags.RetryAttempt.ShouldBe("resilience.retry.attempt");
	}

	#endregion

	[Fact]
	public void AllInstrumentNames_FollowSemanticConventions()
	{
		// All instrument names should start with "dispatch.resilience."
		ResilienceTelemetryConstants.Instruments.RetryAttempts.ShouldStartWith("dispatch.resilience.");
		ResilienceTelemetryConstants.Instruments.CircuitBreakerTransitions.ShouldStartWith("dispatch.resilience.");
		ResilienceTelemetryConstants.Instruments.OperationDuration.ShouldStartWith("dispatch.resilience.");
		ResilienceTelemetryConstants.Instruments.Timeouts.ShouldStartWith("dispatch.resilience.");
		ResilienceTelemetryConstants.Instruments.OperationsExecuted.ShouldStartWith("dispatch.resilience.");
	}

	[Fact]
	public void AllTagNames_FollowSemanticConventions()
	{
		// All tag names should start with "resilience."
		ResilienceTelemetryConstants.Tags.PipelineName.ShouldStartWith("resilience.");
		ResilienceTelemetryConstants.Tags.StrategyType.ShouldStartWith("resilience.");
		ResilienceTelemetryConstants.Tags.Outcome.ShouldStartWith("resilience.");
		ResilienceTelemetryConstants.Tags.CircuitState.ShouldStartWith("resilience.");
		ResilienceTelemetryConstants.Tags.RetryAttempt.ShouldStartWith("resilience.");
	}
}
