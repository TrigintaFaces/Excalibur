// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Dispatch.Hosting.Tests.AwsLambda.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AwsLambdaEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.AwsLambda")]
[Trait("Priority", "0")]
public sealed class AwsLambdaEventIdShould : UnitTestBase
{
	#region Lambda Host Provider Event ID Tests (50100-50149)

	[Fact]
	public void HaveConfiguringServicesInHostProviderRange()
	{
		AwsLambdaEventId.ConfiguringServices.ShouldBe(50100);
	}

	[Fact]
	public void HaveServicesConfiguredInHostProviderRange()
	{
		AwsLambdaEventId.ServicesConfigured.ShouldBe(50101);
	}

	[Fact]
	public void HaveConfiguringHostInHostProviderRange()
	{
		AwsLambdaEventId.ConfiguringHost.ShouldBe(50102);
	}

	[Fact]
	public void HaveHostConfiguredInHostProviderRange()
	{
		AwsLambdaEventId.HostConfigured.ShouldBe(50103);
	}

	[Fact]
	public void HaveExecutingHandlerInHostProviderRange()
	{
		AwsLambdaEventId.ExecutingHandler.ShouldBe(50104);
	}

	[Fact]
	public void HaveHandlerExecutedInHostProviderRange()
	{
		AwsLambdaEventId.HandlerExecuted.ShouldBe(50105);
	}

	[Fact]
	public void HaveExecutionCancelledInHostProviderRange()
	{
		AwsLambdaEventId.ExecutionCancelled.ShouldBe(50106);
	}

	[Fact]
	public void HaveExecutionTimedOutInHostProviderRange()
	{
		AwsLambdaEventId.ExecutionTimedOut.ShouldBe(50107);
	}

	[Fact]
	public void HaveHandlerFailedInHostProviderRange()
	{
		AwsLambdaEventId.HandlerFailed.ShouldBe(50108);
	}

	[Fact]
	public void HaveConfiguringXRayTracingInHostProviderRange()
	{
		AwsLambdaEventId.ConfiguringXRayTracing.ShouldBe(50109);
	}

	[Fact]
	public void HaveConfiguringMetricsInHostProviderRange()
	{
		AwsLambdaEventId.ConfiguringMetrics.ShouldBe(50110);
	}

	[Fact]
	public void HaveAllHostProviderEventIdsInExpectedRange()
	{
		AwsLambdaEventId.ConfiguringServices.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ServicesConfigured.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ConfiguringHost.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.HostConfigured.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ExecutingHandler.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.HandlerExecuted.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ExecutionCancelled.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ExecutionTimedOut.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.HandlerFailed.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ConfiguringXRayTracing.ShouldBeInRange(50100, 50149);
		AwsLambdaEventId.ConfiguringMetrics.ShouldBeInRange(50100, 50149);
	}

	#endregion

	#region Lambda Cold Start Optimization Event ID Tests (50150-50199)

	[Fact]
	public void HaveColdStartOptimizationStartedInColdStartRange()
	{
		AwsLambdaEventId.ColdStartOptimizationStarted.ShouldBe(50150);
	}

	[Fact]
	public void HaveColdStartOptimizationCompletedInColdStartRange()
	{
		AwsLambdaEventId.ColdStartOptimizationCompleted.ShouldBe(50151);
	}

	[Fact]
	public void HaveWarmInstanceDetectedInColdStartRange()
	{
		AwsLambdaEventId.WarmInstanceDetected.ShouldBe(50152);
	}

	[Fact]
	public void HaveColdInstanceDetectedInColdStartRange()
	{
		AwsLambdaEventId.ColdInstanceDetected.ShouldBe(50153);
	}

	[Fact]
	public void HaveProvisionedConcurrencyActiveInColdStartRange()
	{
		AwsLambdaEventId.ProvisionedConcurrencyActive.ShouldBe(50154);
	}

	[Fact]
	public void HaveColdStartOptimizationDisabledInColdStartRange()
	{
		AwsLambdaEventId.ColdStartOptimizationDisabled.ShouldBe(50155);
	}

	[Fact]
	public void HaveWarmingUpServicesInColdStartRange()
	{
		AwsLambdaEventId.WarmingUpServices.ShouldBe(50156);
	}

	[Fact]
	public void HaveServicesWarmedUpInColdStartRange()
	{
		AwsLambdaEventId.ServicesWarmedUp.ShouldBe(50157);
	}

	[Fact]
	public void HaveAllColdStartEventIdsInExpectedRange()
	{
		AwsLambdaEventId.ColdStartOptimizationStarted.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.ColdStartOptimizationCompleted.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.WarmInstanceDetected.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.ColdInstanceDetected.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.ProvisionedConcurrencyActive.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.ColdStartOptimizationDisabled.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.WarmingUpServices.ShouldBeInRange(50150, 50199);
		AwsLambdaEventId.ServicesWarmedUp.ShouldBeInRange(50150, 50199);
	}

	#endregion

	#region AWS Lambda Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInAwsLambdaReservedRange()
	{
		// AWS Lambda reserved range is 50100-50199
		var allEventIds = GetAllAwsLambdaEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(50100, 50199,
				$"Event ID {eventId} is outside AWS Lambda reserved range (50100-50199)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAwsLambdaEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAwsLambdaEventIds();
		allEventIds.Length.ShouldBeGreaterThan(15);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAwsLambdaEventIds()
	{
		return
		[
			// Host Provider (50100-50149)
			AwsLambdaEventId.ConfiguringServices,
			AwsLambdaEventId.ServicesConfigured,
			AwsLambdaEventId.ConfiguringHost,
			AwsLambdaEventId.HostConfigured,
			AwsLambdaEventId.ExecutingHandler,
			AwsLambdaEventId.HandlerExecuted,
			AwsLambdaEventId.ExecutionCancelled,
			AwsLambdaEventId.ExecutionTimedOut,
			AwsLambdaEventId.HandlerFailed,
			AwsLambdaEventId.ConfiguringXRayTracing,
			AwsLambdaEventId.ConfiguringMetrics,

			// Cold Start Optimization (50150-50199)
			AwsLambdaEventId.ColdStartOptimizationStarted,
			AwsLambdaEventId.ColdStartOptimizationCompleted,
			AwsLambdaEventId.WarmInstanceDetected,
			AwsLambdaEventId.ColdInstanceDetected,
			AwsLambdaEventId.ProvisionedConcurrencyActive,
			AwsLambdaEventId.ColdStartOptimizationDisabled,
			AwsLambdaEventId.WarmingUpServices,
			AwsLambdaEventId.ServicesWarmedUp
		];
	}

	#endregion
}
