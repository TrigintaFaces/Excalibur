// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

namespace Excalibur.Dispatch.Hosting.Tests.AzureFunctions.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AzureFunctionsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.AzureFunctions")]
[Trait("Priority", "0")]
public sealed class AzureFunctionsEventIdShould : UnitTestBase
{
	#region Azure Functions Host Provider Event ID Tests (50200-50249)

	[Fact]
	public void HaveConfiguringServicesInHostProviderRange()
	{
		AzureFunctionsEventId.ConfiguringServices.ShouldBe(50200);
	}

	[Fact]
	public void HaveServicesConfiguredInHostProviderRange()
	{
		AzureFunctionsEventId.ServicesConfigured.ShouldBe(50201);
	}

	[Fact]
	public void HaveSupportNotAvailableInHostProviderRange()
	{
		AzureFunctionsEventId.SupportNotAvailable.ShouldBe(50202);
	}

	[Fact]
	public void HaveConfiguringHostInHostProviderRange()
	{
		AzureFunctionsEventId.ConfiguringHost.ShouldBe(50203);
	}

	[Fact]
	public void HaveHostConfiguredInHostProviderRange()
	{
		AzureFunctionsEventId.HostConfigured.ShouldBe(50204);
	}

	[Fact]
	public void HaveExecutingHandlerInHostProviderRange()
	{
		AzureFunctionsEventId.ExecutingHandler.ShouldBe(50205);
	}

	[Fact]
	public void HaveHandlerExecutedInHostProviderRange()
	{
		AzureFunctionsEventId.HandlerExecuted.ShouldBe(50206);
	}

	[Fact]
	public void HaveExecutionCancelledInHostProviderRange()
	{
		AzureFunctionsEventId.ExecutionCancelled.ShouldBe(50207);
	}

	[Fact]
	public void HaveExecutionTimedOutInHostProviderRange()
	{
		AzureFunctionsEventId.ExecutionTimedOut.ShouldBe(50208);
	}

	[Fact]
	public void HaveHandlerFailedInHostProviderRange()
	{
		AzureFunctionsEventId.HandlerFailed.ShouldBe(50209);
	}

	[Fact]
	public void HaveConfiguringAppInsightsInHostProviderRange()
	{
		AzureFunctionsEventId.ConfiguringAppInsights.ShouldBe(50210);
	}

	[Fact]
	public void HaveConfiguringMetricsInHostProviderRange()
	{
		AzureFunctionsEventId.ConfiguringMetrics.ShouldBe(50211);
	}

	[Fact]
	public void HaveConfiguringDurableFunctionsInHostProviderRange()
	{
		AzureFunctionsEventId.ConfiguringDurableFunctions.ShouldBe(50212);
	}

	[Fact]
	public void HaveAllHostProviderEventIdsInExpectedRange()
	{
		AzureFunctionsEventId.ConfiguringServices.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ServicesConfigured.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.SupportNotAvailable.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ConfiguringHost.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.HostConfigured.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ExecutingHandler.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.HandlerExecuted.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ExecutionCancelled.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ExecutionTimedOut.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.HandlerFailed.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ConfiguringAppInsights.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ConfiguringMetrics.ShouldBeInRange(50200, 50249);
		AzureFunctionsEventId.ConfiguringDurableFunctions.ShouldBeInRange(50200, 50249);
	}

	#endregion

	#region Azure Functions Cold Start Optimization Event ID Tests (50250-50299)

	[Fact]
	public void HaveColdStartOptimizationStartedInColdStartRange()
	{
		AzureFunctionsEventId.ColdStartOptimizationStarted.ShouldBe(50250);
	}

	[Fact]
	public void HaveColdStartOptimizationCompletedInColdStartRange()
	{
		AzureFunctionsEventId.ColdStartOptimizationCompleted.ShouldBe(50251);
	}

	[Fact]
	public void HaveWarmInstanceDetectedInColdStartRange()
	{
		AzureFunctionsEventId.WarmInstanceDetected.ShouldBe(50252);
	}

	[Fact]
	public void HaveColdInstanceDetectedInColdStartRange()
	{
		AzureFunctionsEventId.ColdInstanceDetected.ShouldBe(50253);
	}

	[Fact]
	public void HavePremiumPlanActiveInColdStartRange()
	{
		AzureFunctionsEventId.PremiumPlanActive.ShouldBe(50254);
	}

	[Fact]
	public void HaveColdStartOptimizationDisabledInColdStartRange()
	{
		AzureFunctionsEventId.ColdStartOptimizationDisabled.ShouldBe(50255);
	}

	[Fact]
	public void HaveWarmingUpServicesInColdStartRange()
	{
		AzureFunctionsEventId.WarmingUpServices.ShouldBe(50256);
	}

	[Fact]
	public void HaveServicesWarmedUpInColdStartRange()
	{
		AzureFunctionsEventId.ServicesWarmedUp.ShouldBe(50257);
	}

	[Fact]
	public void HaveAllColdStartEventIdsInExpectedRange()
	{
		AzureFunctionsEventId.ColdStartOptimizationStarted.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.ColdStartOptimizationCompleted.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.WarmInstanceDetected.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.ColdInstanceDetected.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.PremiumPlanActive.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.ColdStartOptimizationDisabled.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.WarmingUpServices.ShouldBeInRange(50250, 50299);
		AzureFunctionsEventId.ServicesWarmedUp.ShouldBeInRange(50250, 50299);
	}

	#endregion

	#region Durable Functions Orchestration Event ID Tests (50300-50399)

	[Fact]
	public void HaveOrchestrationStartedInOrchestrationRange()
	{
		AzureFunctionsEventId.OrchestrationStarted.ShouldBe(50300);
	}

	[Fact]
	public void HaveOrchestrationCompletedInOrchestrationRange()
	{
		AzureFunctionsEventId.OrchestrationCompleted.ShouldBe(50301);
	}

	[Fact]
	public void HaveOrchestrationFailedInOrchestrationRange()
	{
		AzureFunctionsEventId.OrchestrationFailed.ShouldBe(50302);
	}

	[Fact]
	public void HaveActivityStartedInOrchestrationRange()
	{
		AzureFunctionsEventId.ActivityStarted.ShouldBe(50303);
	}

	[Fact]
	public void HaveActivityCompletedInOrchestrationRange()
	{
		AzureFunctionsEventId.ActivityCompleted.ShouldBe(50304);
	}

	[Fact]
	public void HaveActivityFailedInOrchestrationRange()
	{
		AzureFunctionsEventId.ActivityFailed.ShouldBe(50305);
	}

	[Fact]
	public void HaveAllOrchestrationEventIdsInExpectedRange()
	{
		AzureFunctionsEventId.OrchestrationStarted.ShouldBeInRange(50300, 50399);
		AzureFunctionsEventId.OrchestrationCompleted.ShouldBeInRange(50300, 50399);
		AzureFunctionsEventId.OrchestrationFailed.ShouldBeInRange(50300, 50399);
		AzureFunctionsEventId.ActivityStarted.ShouldBeInRange(50300, 50399);
		AzureFunctionsEventId.ActivityCompleted.ShouldBeInRange(50300, 50399);
		AzureFunctionsEventId.ActivityFailed.ShouldBeInRange(50300, 50399);
	}

	#endregion

	#region Azure Functions Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInAzureFunctionsReservedRange()
	{
		// Azure Functions reserved range is 50200-50399
		var allEventIds = GetAllAzureFunctionsEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(50200, 50399,
				$"Event ID {eventId} is outside Azure Functions reserved range (50200-50399)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAzureFunctionsEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAzureFunctionsEventIds();
		allEventIds.Length.ShouldBeGreaterThan(25);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAzureFunctionsEventIds()
	{
		return
		[
			// Host Provider (50200-50249)
			AzureFunctionsEventId.ConfiguringServices,
			AzureFunctionsEventId.ServicesConfigured,
			AzureFunctionsEventId.SupportNotAvailable,
			AzureFunctionsEventId.ConfiguringHost,
			AzureFunctionsEventId.HostConfigured,
			AzureFunctionsEventId.ExecutingHandler,
			AzureFunctionsEventId.HandlerExecuted,
			AzureFunctionsEventId.ExecutionCancelled,
			AzureFunctionsEventId.ExecutionTimedOut,
			AzureFunctionsEventId.HandlerFailed,
			AzureFunctionsEventId.ConfiguringAppInsights,
			AzureFunctionsEventId.ConfiguringMetrics,
			AzureFunctionsEventId.ConfiguringDurableFunctions,

			// Cold Start Optimization (50250-50299)
			AzureFunctionsEventId.ColdStartOptimizationStarted,
			AzureFunctionsEventId.ColdStartOptimizationCompleted,
			AzureFunctionsEventId.WarmInstanceDetected,
			AzureFunctionsEventId.ColdInstanceDetected,
			AzureFunctionsEventId.PremiumPlanActive,
			AzureFunctionsEventId.ColdStartOptimizationDisabled,
			AzureFunctionsEventId.WarmingUpServices,
			AzureFunctionsEventId.ServicesWarmedUp,

			// Durable Functions Orchestration (50300-50399)
			AzureFunctionsEventId.OrchestrationStarted,
			AzureFunctionsEventId.OrchestrationCompleted,
			AzureFunctionsEventId.OrchestrationFailed,
			AzureFunctionsEventId.ActivityStarted,
			AzureFunctionsEventId.ActivityCompleted,
			AzureFunctionsEventId.ActivityFailed
		];
	}

	#endregion
}
