// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ResilienceEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Resilience")]
[Trait("Priority", "0")]
public sealed class ResilienceEventIdShould : UnitTestBase
{
	#region Circuit Breaker Core Event ID Tests (60000-60099)

	[Fact]
	public void HaveCircuitBreakerFactoryCreatedInCoreRange()
	{
		ResilienceEventId.CircuitBreakerFactoryCreated.ShouldBe(60000);
	}

	[Fact]
	public void HaveCircuitBreakerAdapterCreatedInCoreRange()
	{
		ResilienceEventId.CircuitBreakerAdapterCreated.ShouldBe(60001);
	}

	[Fact]
	public void HaveCircuitBreakerStateChangedInCoreRange()
	{
		ResilienceEventId.CircuitBreakerStateChanged.ShouldBe(60002);
	}

	[Fact]
	public void HaveCircuitBreakerOpenedInCoreRange()
	{
		ResilienceEventId.CircuitBreakerOpened.ShouldBe(60003);
	}

	[Fact]
	public void HaveCircuitBreakerClosedInCoreRange()
	{
		ResilienceEventId.CircuitBreakerClosed.ShouldBe(60004);
	}

	[Fact]
	public void HaveAllCircuitBreakerCoreEventIdsInExpectedRange()
	{
		ResilienceEventId.CircuitBreakerFactoryCreated.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerAdapterCreated.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerStateChanged.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerOpened.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerClosed.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerHalfOpen.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerReset.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerRejected.ShouldBeInRange(60000, 60099);
		ResilienceEventId.CircuitBreakerStopError.ShouldBeInRange(60000, 60099);
	}

	#endregion

	#region Circuit Breaker Policy Event ID Tests (60100-60199)

	[Fact]
	public void HaveCircuitBreakerPolicyCreatedInPolicyRange()
	{
		ResilienceEventId.CircuitBreakerPolicyCreated.ShouldBe(60100);
	}

	[Fact]
	public void HaveDistributedCircuitBreakerSyncedInPolicyRange()
	{
		ResilienceEventId.DistributedCircuitBreakerSynced.ShouldBe(60102);
	}

	[Fact]
	public void HavePollyCircuitBreakerCreatedInPolicyRange()
	{
		ResilienceEventId.PollyCircuitBreakerCreated.ShouldBe(60104);
	}

	[Fact]
	public void HaveAllPolicyEventIdsInExpectedRange()
	{
		ResilienceEventId.CircuitBreakerPolicyCreated.ShouldBeInRange(60100, 60199);
		ResilienceEventId.CircuitBreakerPolicyAdapterCreated.ShouldBeInRange(60100, 60199);
		ResilienceEventId.DistributedCircuitBreakerSynced.ShouldBeInRange(60100, 60199);
		ResilienceEventId.DistributedCircuitBreakerConflict.ShouldBeInRange(60100, 60199);
		ResilienceEventId.PollyCircuitBreakerCreated.ShouldBeInRange(60100, 60199);
		ResilienceEventId.PollyCircuitBreakerRemoved.ShouldBeInRange(60100, 60199);
	}

	#endregion

	#region Circuit Breaker Registry Event ID Tests (60200-60299)

	[Fact]
	public void HaveTransportCircuitBreakerRegisteredInRegistryRange()
	{
		ResilienceEventId.TransportCircuitBreakerRegistered.ShouldBe(60200);
	}

	[Fact]
	public void HaveAllCircuitBreakersResetInRegistryRange()
	{
		ResilienceEventId.AllCircuitBreakersReset.ShouldBe(60204);
	}

	[Fact]
	public void HaveAllRegistryEventIdsInExpectedRange()
	{
		ResilienceEventId.TransportCircuitBreakerRegistered.ShouldBeInRange(60200, 60299);
		ResilienceEventId.TransportCircuitBreakerUnregistered.ShouldBeInRange(60200, 60299);
		ResilienceEventId.TransportCircuitBreakerRetrieved.ShouldBeInRange(60200, 60299);
		ResilienceEventId.TransportCircuitBreakerNotFound.ShouldBeInRange(60200, 60299);
		ResilienceEventId.AllCircuitBreakersReset.ShouldBeInRange(60200, 60299);
	}

	#endregion

	#region Retry Policy Event ID Tests (60300-60399)

	[Fact]
	public void HaveRetryPolicyCreatedInRetryRange()
	{
		ResilienceEventId.RetryPolicyCreated.ShouldBe(60300);
	}

	[Fact]
	public void HaveRetrySucceededInRetryRange()
	{
		ResilienceEventId.RetrySucceeded.ShouldBe(60304);
	}

	[Fact]
	public void HaveRetryExhaustedInRetryRange()
	{
		ResilienceEventId.RetryExhausted.ShouldBe(60305);
	}

	[Fact]
	public void HaveAllRetryEventIdsInExpectedRange()
	{
		ResilienceEventId.RetryPolicyCreated.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetryPolicyFactoryCreated.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetryPolicyAdapterCreated.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetryAttemptStarted.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetrySucceeded.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetryExhausted.ShouldBeInRange(60300, 60399);
		ResilienceEventId.RetryOperationTimeout.ShouldBeInRange(60300, 60399);
	}

	#endregion

	#region Bulkhead Event ID Tests (60400-60499)

	[Fact]
	public void HaveBulkheadPolicyCreatedInBulkheadRange()
	{
		ResilienceEventId.BulkheadPolicyCreated.ShouldBe(60400);
	}

	[Fact]
	public void HaveBulkheadExecutionAllowedInBulkheadRange()
	{
		ResilienceEventId.BulkheadExecutionAllowed.ShouldBe(60401);
	}

	[Fact]
	public void HaveBulkheadExecutionRejectedInBulkheadRange()
	{
		ResilienceEventId.BulkheadExecutionRejected.ShouldBe(60402);
	}

	[Fact]
	public void HaveAllBulkheadEventIdsInExpectedRange()
	{
		ResilienceEventId.BulkheadPolicyCreated.ShouldBeInRange(60400, 60499);
		ResilienceEventId.BulkheadExecutionAllowed.ShouldBeInRange(60400, 60499);
		ResilienceEventId.BulkheadExecutionRejected.ShouldBeInRange(60400, 60499);
		ResilienceEventId.BulkheadQueueFull.ShouldBeInRange(60400, 60499);
		ResilienceEventId.BulkheadSlotAcquired.ShouldBeInRange(60400, 60499);
		ResilienceEventId.BulkheadQueueing.ShouldBeInRange(60400, 60499);
	}

	#endregion

	#region Timeout Event ID Tests (60500-60599)

	[Fact]
	public void HaveTimeoutManagerCreatedInTimeoutRange()
	{
		ResilienceEventId.TimeoutManagerCreated.ShouldBe(60500);
	}

	[Fact]
	public void HaveTimeoutExceededInTimeoutRange()
	{
		ResilienceEventId.TimeoutExceeded.ShouldBe(60504);
	}

	[Fact]
	public void HaveAllTimeoutEventIdsInExpectedRange()
	{
		ResilienceEventId.TimeoutManagerCreated.ShouldBeInRange(60500, 60599);
		ResilienceEventId.TimeoutConfigured.ShouldBeInRange(60500, 60599);
		ResilienceEventId.TimeoutStarted.ShouldBeInRange(60500, 60599);
		ResilienceEventId.TimeoutCompleted.ShouldBeInRange(60500, 60599);
		ResilienceEventId.TimeoutExceeded.ShouldBeInRange(60500, 60599);
		ResilienceEventId.TimeoutCancelled.ShouldBeInRange(60500, 60599);
		ResilienceEventId.SlowOperationDetected.ShouldBeInRange(60500, 60599);
	}

	#endregion

	#region Graceful Degradation Event ID Tests (60600-60699)

	[Fact]
	public void HaveGracefulDegradationCreatedInDegradationRange()
	{
		ResilienceEventId.GracefulDegradationCreated.ShouldBe(60600);
	}

	[Fact]
	public void HaveFallbackExecutedInDegradationRange()
	{
		ResilienceEventId.FallbackExecuted.ShouldBe(60603);
	}

	[Fact]
	public void HaveAllDegradationEventIdsInExpectedRange()
	{
		ResilienceEventId.GracefulDegradationCreated.ShouldBeInRange(60600, 60699);
		ResilienceEventId.GracefulDegradationActivated.ShouldBeInRange(60600, 60699);
		ResilienceEventId.GracefulDegradationDeactivated.ShouldBeInRange(60600, 60699);
		ResilienceEventId.FallbackExecuted.ShouldBeInRange(60600, 60699);
		ResilienceEventId.DegradedResponseReturned.ShouldBeInRange(60600, 60699);
		ResilienceEventId.DegradationHealthCheckError.ShouldBeInRange(60600, 60699);
	}

	#endregion

	#region Default Retry Event ID Tests (60700-60799)

	[Fact]
	public void HaveDefaultRetryPolicyAppliedInDefaultRetryRange()
	{
		ResilienceEventId.DefaultRetryPolicyApplied.ShouldBe(60700);
	}

	[Fact]
	public void HaveDefaultRetryCompletedInDefaultRetryRange()
	{
		ResilienceEventId.DefaultRetryCompleted.ShouldBe(60703);
	}

	[Fact]
	public void HaveAllDefaultRetryEventIdsInExpectedRange()
	{
		ResilienceEventId.DefaultRetryPolicyApplied.ShouldBeInRange(60700, 60799);
		ResilienceEventId.DefaultRetryConfigurationLoaded.ShouldBeInRange(60700, 60799);
		ResilienceEventId.DefaultRetryAttempt.ShouldBeInRange(60700, 60799);
		ResilienceEventId.DefaultRetryCompleted.ShouldBeInRange(60700, 60799);
	}

	#endregion

	#region Resilience Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInResilienceReservedRange()
	{
		// Resilience reserved range is 60000-60999
		var allEventIds = GetAllResilienceEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(60000, 60999,
				$"Event ID {eventId} is outside Resilience reserved range (60000-60999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllResilienceEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllResilienceEventIds();
		allEventIds.Length.ShouldBeGreaterThan(50);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllResilienceEventIds()
	{
		return
		[
			// Circuit Breaker Core (60000-60099)
			ResilienceEventId.CircuitBreakerFactoryCreated,
			ResilienceEventId.CircuitBreakerAdapterCreated,
			ResilienceEventId.CircuitBreakerStateChanged,
			ResilienceEventId.CircuitBreakerOpened,
			ResilienceEventId.CircuitBreakerClosed,
			ResilienceEventId.CircuitBreakerHalfOpen,
			ResilienceEventId.CircuitBreakerReset,
			ResilienceEventId.CircuitBreakerRejected,
			ResilienceEventId.CircuitBreakerFallbackExecuted,
			ResilienceEventId.CircuitBreakerOperationFailed,
			ResilienceEventId.CircuitBreakerThresholdExceeded,
			ResilienceEventId.CircuitBreakerCoordinationError,
			ResilienceEventId.CircuitBreakerResetRequested,
			ResilienceEventId.CircuitBreakerInitializing,
			ResilienceEventId.CircuitBreakerStarting,
			ResilienceEventId.CircuitBreakerStopping,
			ResilienceEventId.CircuitBreakerObserverSubscribed,
			ResilienceEventId.CircuitBreakerObserverUnsubscribed,
			ResilienceEventId.CircuitBreakerStopError,

			// Circuit Breaker Policy (60100-60199)
			ResilienceEventId.CircuitBreakerPolicyCreated,
			ResilienceEventId.CircuitBreakerPolicyAdapterCreated,
			ResilienceEventId.DistributedCircuitBreakerSynced,
			ResilienceEventId.DistributedCircuitBreakerConflict,
			ResilienceEventId.PollyCircuitBreakerCreated,
			ResilienceEventId.PollyCircuitBreakerRemoved,

			// Circuit Breaker Registry (60200-60299)
			ResilienceEventId.TransportCircuitBreakerRegistered,
			ResilienceEventId.TransportCircuitBreakerUnregistered,
			ResilienceEventId.TransportCircuitBreakerRetrieved,
			ResilienceEventId.TransportCircuitBreakerNotFound,
			ResilienceEventId.AllCircuitBreakersReset,

			// Retry Policy (60300-60399)
			ResilienceEventId.RetryPolicyCreated,
			ResilienceEventId.RetryPolicyFactoryCreated,
			ResilienceEventId.RetryPolicyAdapterCreated,
			ResilienceEventId.RetryAttemptStarted,
			ResilienceEventId.RetrySucceeded,
			ResilienceEventId.RetryExhausted,
			ResilienceEventId.RetryDelayCalculated,
			ResilienceEventId.RetryWithJitter,
			ResilienceEventId.RetryMaxExceeded,
			ResilienceEventId.JitterStrategyUsed,
			ResilienceEventId.RetryOperationTimeout,

			// Bulkhead (60400-60499)
			ResilienceEventId.BulkheadPolicyCreated,
			ResilienceEventId.BulkheadExecutionAllowed,
			ResilienceEventId.BulkheadExecutionRejected,
			ResilienceEventId.BulkheadQueueFull,
			ResilienceEventId.BulkheadSlotAcquired,
			ResilienceEventId.BulkheadSlotReleased,
			ResilienceEventId.BulkheadExecuting,
			ResilienceEventId.BulkheadCompleted,
			ResilienceEventId.BulkheadQueueing,

			// Timeout (60500-60599)
			ResilienceEventId.TimeoutManagerCreated,
			ResilienceEventId.TimeoutConfigured,
			ResilienceEventId.TimeoutStarted,
			ResilienceEventId.TimeoutCompleted,
			ResilienceEventId.TimeoutExceeded,
			ResilienceEventId.TimeoutCancelled,
			ResilienceEventId.TimeoutRetrieved,
			ResilienceEventId.TimeoutRegistered,
			ResilienceEventId.SlowOperationDetected,

			// Graceful Degradation (60600-60699)
			ResilienceEventId.GracefulDegradationCreated,
			ResilienceEventId.GracefulDegradationActivated,
			ResilienceEventId.GracefulDegradationDeactivated,
			ResilienceEventId.FallbackExecuted,
			ResilienceEventId.DegradedResponseReturned,
			ResilienceEventId.DegradationLevelChanged,
			ResilienceEventId.DegradationOperationRejected,
			ResilienceEventId.DegradationHealthMetricsUpdated,
			ResilienceEventId.DegradationPrimaryOperationFailed,
			ResilienceEventId.DegradationHealthCheckError,

			// Default Retry (60700-60799)
			ResilienceEventId.DefaultRetryPolicyApplied,
			ResilienceEventId.DefaultRetryConfigurationLoaded,
			ResilienceEventId.DefaultRetryAttempt,
			ResilienceEventId.DefaultRetryCompleted
		];
	}

	#endregion
}
