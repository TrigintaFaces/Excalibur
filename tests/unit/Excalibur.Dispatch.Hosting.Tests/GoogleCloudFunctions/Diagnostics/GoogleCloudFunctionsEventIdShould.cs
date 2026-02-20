// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloudFunctions;

namespace Excalibur.Dispatch.Hosting.Tests.GoogleCloudFunctions.Diagnostics;

/// <summary>
/// Unit tests for <see cref="GoogleCloudFunctionsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.GoogleCloudFunctions")]
[Trait("Priority", "0")]
public sealed class GoogleCloudFunctionsEventIdShould : UnitTestBase
{
	#region Google Cloud Functions Host Provider Event ID Tests (50400-50449)

	[Fact]
	public void HaveConfiguringServicesInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ConfiguringServices.ShouldBe(50400);
	}

	[Fact]
	public void HaveServicesConfiguredInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ServicesConfigured.ShouldBe(50401);
	}

	[Fact]
	public void HaveConfiguringHostInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ConfiguringHost.ShouldBe(50402);
	}

	[Fact]
	public void HaveHostConfiguredInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.HostConfigured.ShouldBe(50403);
	}

	[Fact]
	public void HaveExecutingHandlerInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ExecutingHandler.ShouldBe(50404);
	}

	[Fact]
	public void HaveHandlerExecutedInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.HandlerExecuted.ShouldBe(50405);
	}

	[Fact]
	public void HaveExecutionCancelledInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ExecutionCancelled.ShouldBe(50406);
	}

	[Fact]
	public void HaveExecutionTimedOutInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ExecutionTimedOut.ShouldBe(50407);
	}

	[Fact]
	public void HaveHandlerFailedInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.HandlerFailed.ShouldBe(50408);
	}

	[Fact]
	public void HaveConfiguringCloudTraceInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ConfiguringCloudTrace.ShouldBe(50409);
	}

	[Fact]
	public void HaveConfiguringCloudMonitoringInHostProviderRange()
	{
		GoogleCloudFunctionsEventId.ConfiguringCloudMonitoring.ShouldBe(50410);
	}

	[Fact]
	public void HaveAllHostProviderEventIdsInExpectedRange()
	{
		GoogleCloudFunctionsEventId.ConfiguringServices.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ServicesConfigured.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ConfiguringHost.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.HostConfigured.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ExecutingHandler.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.HandlerExecuted.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ExecutionCancelled.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ExecutionTimedOut.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.HandlerFailed.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ConfiguringCloudTrace.ShouldBeInRange(50400, 50449);
		GoogleCloudFunctionsEventId.ConfiguringCloudMonitoring.ShouldBeInRange(50400, 50449);
	}

	#endregion

	#region Google Cloud Functions Cold Start Optimization Event ID Tests (50450-50499)

	[Fact]
	public void HaveColdStartOptimizationStartedInColdStartRange()
	{
		GoogleCloudFunctionsEventId.ColdStartOptimizationStarted.ShouldBe(50450);
	}

	[Fact]
	public void HaveColdStartOptimizationCompletedInColdStartRange()
	{
		GoogleCloudFunctionsEventId.ColdStartOptimizationCompleted.ShouldBe(50451);
	}

	[Fact]
	public void HaveWarmInstanceDetectedInColdStartRange()
	{
		GoogleCloudFunctionsEventId.WarmInstanceDetected.ShouldBe(50452);
	}

	[Fact]
	public void HaveColdInstanceDetectedInColdStartRange()
	{
		GoogleCloudFunctionsEventId.ColdInstanceDetected.ShouldBe(50453);
	}

	[Fact]
	public void HaveMinimumInstancesConfiguredInColdStartRange()
	{
		GoogleCloudFunctionsEventId.MinimumInstancesConfigured.ShouldBe(50454);
	}

	[Fact]
	public void HaveColdStartOptimizationDisabledInColdStartRange()
	{
		GoogleCloudFunctionsEventId.ColdStartOptimizationDisabled.ShouldBe(50455);
	}

	[Fact]
	public void HaveWarmingUpServicesInColdStartRange()
	{
		GoogleCloudFunctionsEventId.WarmingUpServices.ShouldBe(50456);
	}

	[Fact]
	public void HaveServicesWarmedUpInColdStartRange()
	{
		GoogleCloudFunctionsEventId.ServicesWarmedUp.ShouldBe(50457);
	}

	[Fact]
	public void HaveAllColdStartEventIdsInExpectedRange()
	{
		GoogleCloudFunctionsEventId.ColdStartOptimizationStarted.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.ColdStartOptimizationCompleted.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.WarmInstanceDetected.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.ColdInstanceDetected.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.MinimumInstancesConfigured.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.ColdStartOptimizationDisabled.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.WarmingUpServices.ShouldBeInRange(50450, 50499);
		GoogleCloudFunctionsEventId.ServicesWarmedUp.ShouldBeInRange(50450, 50499);
	}

	#endregion

	#region Google Cloud Functions Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInGoogleCloudFunctionsReservedRange()
	{
		// Google Cloud Functions reserved range is 50400-50499
		var allEventIds = GetAllGoogleCloudFunctionsEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(50400, 50499,
				$"Event ID {eventId} is outside Google Cloud Functions reserved range (50400-50499)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllGoogleCloudFunctionsEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllGoogleCloudFunctionsEventIds();
		allEventIds.Length.ShouldBeGreaterThan(15);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllGoogleCloudFunctionsEventIds()
	{
		return
		[
			// Host Provider (50400-50449)
			GoogleCloudFunctionsEventId.ConfiguringServices,
			GoogleCloudFunctionsEventId.ServicesConfigured,
			GoogleCloudFunctionsEventId.ConfiguringHost,
			GoogleCloudFunctionsEventId.HostConfigured,
			GoogleCloudFunctionsEventId.ExecutingHandler,
			GoogleCloudFunctionsEventId.HandlerExecuted,
			GoogleCloudFunctionsEventId.ExecutionCancelled,
			GoogleCloudFunctionsEventId.ExecutionTimedOut,
			GoogleCloudFunctionsEventId.HandlerFailed,
			GoogleCloudFunctionsEventId.ConfiguringCloudTrace,
			GoogleCloudFunctionsEventId.ConfiguringCloudMonitoring,

			// Cold Start Optimization (50450-50499)
			GoogleCloudFunctionsEventId.ColdStartOptimizationStarted,
			GoogleCloudFunctionsEventId.ColdStartOptimizationCompleted,
			GoogleCloudFunctionsEventId.WarmInstanceDetected,
			GoogleCloudFunctionsEventId.ColdInstanceDetected,
			GoogleCloudFunctionsEventId.MinimumInstancesConfigured,
			GoogleCloudFunctionsEventId.ColdStartOptimizationDisabled,
			GoogleCloudFunctionsEventId.WarmingUpServices,
			GoogleCloudFunctionsEventId.ServicesWarmedUp
		];
	}

	#endregion
}
