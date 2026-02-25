// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Hosting.Tests.ServerlessAbstractions.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ServerlessEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Serverless")]
[Trait("Priority", "0")]
public sealed class ServerlessEventIdShould : UnitTestBase
{
	#region Serverless Host Provider Event ID Tests (50000-50049)

	[Fact]
	public void HaveHostingServiceStartingInHostProviderRange()
	{
		ServerlessEventId.HostingServiceStarting.ShouldBe(50000);
	}

	[Fact]
	public void HaveHostingServiceStoppingInHostProviderRange()
	{
		ServerlessEventId.HostingServiceStopping.ShouldBe(50001);
	}

	[Fact]
	public void HaveProviderUnavailableInHostProviderRange()
	{
		ServerlessEventId.ProviderUnavailable.ShouldBe(50002);
	}

	[Fact]
	public void HaveProviderReadyInHostProviderRange()
	{
		ServerlessEventId.ProviderReady.ShouldBe(50003);
	}

	[Fact]
	public void HaveContextCreatedInHostProviderRange()
	{
		ServerlessEventId.ContextCreated.ShouldBe(50004);
	}

	[Fact]
	public void HaveExecutionStartedInHostProviderRange()
	{
		ServerlessEventId.ExecutionStarted.ShouldBe(50005);
	}

	[Fact]
	public void HaveExecutionCompletedInHostProviderRange()
	{
		ServerlessEventId.ExecutionCompleted.ShouldBe(50006);
	}

	[Fact]
	public void HaveExecutionFailedInHostProviderRange()
	{
		ServerlessEventId.ExecutionFailed.ShouldBe(50007);
	}

	[Fact]
	public void HaveExecutionTimedOutInHostProviderRange()
	{
		ServerlessEventId.ExecutionTimedOut.ShouldBe(50008);
	}

	[Fact]
	public void HaveExecutionCancelledInHostProviderRange()
	{
		ServerlessEventId.ExecutionCancelled.ShouldBe(50009);
	}

	[Fact]
	public void HaveAllHostProviderEventIdsInExpectedRange()
	{
		ServerlessEventId.HostingServiceStarting.ShouldBeInRange(50000, 50049);
		ServerlessEventId.HostingServiceStopping.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ProviderUnavailable.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ProviderReady.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ContextCreated.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionStarted.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionCompleted.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionFailed.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionTimedOut.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionCancelled.ShouldBeInRange(50000, 50049);
	}

	#endregion

	#region Serverless Host Factory Event ID Tests (50050-50099)

	[Fact]
	public void HaveProviderFactoryCreatedInFactoryRange()
	{
		ServerlessEventId.ProviderFactoryCreated.ShouldBe(50050);
	}

	[Fact]
	public void HaveProviderRegisteredInFactoryRange()
	{
		ServerlessEventId.ProviderRegistered.ShouldBe(50051);
	}

	[Fact]
	public void HaveProviderResolvedInFactoryRange()
	{
		ServerlessEventId.ProviderResolved.ShouldBe(50052);
	}

	[Fact]
	public void HaveColdStartOptimizationEnabledInFactoryRange()
	{
		ServerlessEventId.ColdStartOptimizationEnabled.ShouldBe(50053);
	}

	[Fact]
	public void HaveColdStartOptimizationCompletedInFactoryRange()
	{
		ServerlessEventId.ColdStartOptimizationCompleted.ShouldBe(50054);
	}

	[Fact]
	public void HavePlatformSelectedInFactoryRange()
	{
		ServerlessEventId.PlatformSelected.ShouldBe(50055);
	}

	[Fact]
	public void HavePlatformFallbackInFactoryRange()
	{
		ServerlessEventId.PlatformFallback.ShouldBe(50056);
	}

	[Fact]
	public void HaveUnableToDetectPlatformInFactoryRange()
	{
		ServerlessEventId.UnableToDetectPlatform.ShouldBe(50057);
	}

	[Fact]
	public void HaveAllHostFactoryEventIdsInExpectedRange()
	{
		ServerlessEventId.ProviderFactoryCreated.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ProviderRegistered.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ProviderResolved.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ColdStartOptimizationEnabled.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ColdStartOptimizationCompleted.ShouldBeInRange(50050, 50099);
		ServerlessEventId.PlatformSelected.ShouldBeInRange(50050, 50099);
		ServerlessEventId.PlatformFallback.ShouldBeInRange(50050, 50099);
		ServerlessEventId.UnableToDetectPlatform.ShouldBeInRange(50050, 50099);
	}

	#endregion

	#region Serverless Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInServerlessReservedRange()
	{
		// Serverless abstractions reserved range is 50000-50099
		var allEventIds = GetAllServerlessEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(50000, 50099,
				$"Event ID {eventId} is outside Serverless reserved range (50000-50099)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllServerlessEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllServerlessEventIds();
		allEventIds.Length.ShouldBeGreaterThan(15);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllServerlessEventIds()
	{
		return
		[
			// Host Provider (50000-50049)
			ServerlessEventId.HostingServiceStarting,
			ServerlessEventId.HostingServiceStopping,
			ServerlessEventId.ProviderUnavailable,
			ServerlessEventId.ProviderReady,
			ServerlessEventId.ContextCreated,
			ServerlessEventId.ExecutionStarted,
			ServerlessEventId.ExecutionCompleted,
			ServerlessEventId.ExecutionFailed,
			ServerlessEventId.ExecutionTimedOut,
			ServerlessEventId.ExecutionCancelled,

			// Host Factory (50050-50099)
			ServerlessEventId.ProviderFactoryCreated,
			ServerlessEventId.ProviderRegistered,
			ServerlessEventId.ProviderResolved,
			ServerlessEventId.ColdStartOptimizationEnabled,
			ServerlessEventId.ColdStartOptimizationCompleted,
			ServerlessEventId.PlatformSelected,
			ServerlessEventId.PlatformFallback,
			ServerlessEventId.UnableToDetectPlatform
		];
	}

	#endregion
}
