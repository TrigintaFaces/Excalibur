// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="PatternsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Patterns")]
[Trait("Priority", "0")]
public sealed class PatternsEventIdShould : UnitTestBase
{
	#region ClaimCheck Core Event ID Tests (90000-90099)

	[Fact]
	public void HaveClaimCheckCleanupServiceCreatedInClaimCheckCoreRange()
	{
		PatternsEventId.ClaimCheckCleanupServiceCreated.ShouldBe(90000);
	}

	[Fact]
	public void HaveClaimCheckCleanupStartedInClaimCheckCoreRange()
	{
		PatternsEventId.ClaimCheckCleanupStarted.ShouldBe(90001);
	}

	[Fact]
	public void HaveClaimCheckCleanupCompletedInClaimCheckCoreRange()
	{
		PatternsEventId.ClaimCheckCleanupCompleted.ShouldBe(90002);
	}

	[Fact]
	public void HaveClaimStoredInClaimCheckCoreRange()
	{
		PatternsEventId.ClaimStored.ShouldBe(90003);
	}

	[Fact]
	public void HaveClaimRetrievedInClaimCheckCoreRange()
	{
		PatternsEventId.ClaimRetrieved.ShouldBe(90004);
	}

	[Fact]
	public void HaveAllClaimCheckCoreEventIdsInExpectedRange()
	{
		PatternsEventId.ClaimCheckCleanupServiceCreated.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupStarted.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupCompleted.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimStored.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimRetrieved.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimDeleted.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimExpired.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupDisabled.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupStartedInterval.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupTaskRunning.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupError.ShouldBeInRange(90000, 90099);
		PatternsEventId.ClaimCheckCleanupServiceStopped.ShouldBeInRange(90000, 90099);
	}

	#endregion

	#region ClaimCheck InMemory Event ID Tests (90100-90199)

	[Fact]
	public void HaveInMemoryClaimCheckCleanupCreatedInInMemoryRange()
	{
		PatternsEventId.InMemoryClaimCheckCleanupCreated.ShouldBe(90100);
	}

	[Fact]
	public void HaveInMemoryClaimStoredInInMemoryRange()
	{
		PatternsEventId.InMemoryClaimStored.ShouldBe(90101);
	}

	[Fact]
	public void HaveAllClaimCheckInMemoryEventIdsInExpectedRange()
	{
		PatternsEventId.InMemoryClaimCheckCleanupCreated.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryClaimStored.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryClaimRetrieved.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupCompleted.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupDisabled.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupStartedInterval.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupServiceStopped.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupTaskRunning.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryCleanupError.ShouldBeInRange(90100, 90199);
		PatternsEventId.InMemoryExpiredClaimsRemoved.ShouldBeInRange(90100, 90199);
	}

	#endregion

	#region Routing Patterns Event ID Tests (90200-90299)

	[Fact]
	public void HaveRoutingPolicyEvaluatorCreatedInRoutingRange()
	{
		PatternsEventId.RoutingPolicyEvaluatorCreated.ShouldBe(90200);
	}

	[Fact]
	public void HaveRouteSelectedInRoutingRange()
	{
		PatternsEventId.RouteSelected.ShouldBe(90202);
	}

	[Fact]
	public void HaveAllRoutingPatternsEventIdsInExpectedRange()
	{
		PatternsEventId.RoutingPolicyEvaluatorCreated.ShouldBeInRange(90200, 90299);
		PatternsEventId.RoutingPolicyEvaluated.ShouldBeInRange(90200, 90299);
		PatternsEventId.RouteSelected.ShouldBeInRange(90200, 90299);
		PatternsEventId.TimeZoneAwareRouterCreated.ShouldBeInRange(90200, 90299);
		PatternsEventId.TimeZoneAwareRoutingApplied.ShouldBeInRange(90200, 90299);
		PatternsEventId.NoRouteMatched.ShouldBeInRange(90200, 90299);
		PatternsEventId.RoutingRulesEvaluated.ShouldBeInRange(90200, 90299);
		PatternsEventId.RuleNotApplicableTimeRange.ShouldBeInRange(90200, 90299);
		PatternsEventId.RuleNotApplicableDayOfWeek.ShouldBeInRange(90200, 90299);
		PatternsEventId.RuleNotApplicableDateRange.ShouldBeInRange(90200, 90299);
		PatternsEventId.RuleNotApplicableConditions.ShouldBeInRange(90200, 90299);
		PatternsEventId.RuleIsApplicable.ShouldBeInRange(90200, 90299);
		PatternsEventId.TimezoneNotFoundLocalTime.ShouldBeInRange(90200, 90299);
		PatternsEventId.TimezoneNotFoundUtc.ShouldBeInRange(90200, 90299);
		PatternsEventId.RouteInBusinessHours.ShouldBeInRange(90200, 90299);
		PatternsEventId.RouteOutsideBusinessHours.ShouldBeInRange(90200, 90299);
		PatternsEventId.NoRoutesInBusinessHoursFallback.ShouldBeInRange(90200, 90299);
	}

	#endregion

	#region Load Balancing Event ID Tests (90300-90399)

	[Fact]
	public void HaveWeightedRoundRobinCreatedInLoadBalancingRange()
	{
		PatternsEventId.WeightedRoundRobinCreated.ShouldBe(90300);
	}

	[Fact]
	public void HaveEndpointSelectedInLoadBalancingRange()
	{
		PatternsEventId.EndpointSelected.ShouldBe(90304);
	}

	[Fact]
	public void HaveAllLoadBalancingEventIdsInExpectedRange()
	{
		PatternsEventId.WeightedRoundRobinCreated.ShouldBeInRange(90300, 90399);
		PatternsEventId.LeastConnectionsCreated.ShouldBeInRange(90300, 90399);
		PatternsEventId.ConsistentHashCreated.ShouldBeInRange(90300, 90399);
		PatternsEventId.RandomLoadBalancerCreated.ShouldBeInRange(90300, 90399);
		PatternsEventId.EndpointSelected.ShouldBeInRange(90300, 90399);
		PatternsEventId.EndpointUnavailable.ShouldBeInRange(90300, 90399);
		PatternsEventId.LoadBalancerRebalancing.ShouldBeInRange(90300, 90399);
	}

	#endregion

	#region Route Health Event ID Tests (90400-90499)

	[Fact]
	public void HaveRouteHealthMonitorCreatedInRouteHealthRange()
	{
		PatternsEventId.RouteHealthMonitorCreated.ShouldBe(90400);
	}

	[Fact]
	public void HaveAllRouteHealthEventIdsInExpectedRange()
	{
		PatternsEventId.RouteHealthMonitorCreated.ShouldBeInRange(90400, 90499);
		PatternsEventId.RouteHealthCheckPerformed.ShouldBeInRange(90400, 90499);
		PatternsEventId.RouteMarkedHealthy.ShouldBeInRange(90400, 90499);
		PatternsEventId.RouteMarkedUnhealthy.ShouldBeInRange(90400, 90499);
		PatternsEventId.RouteHealthStatusChanged.ShouldBeInRange(90400, 90499);
	}

	#endregion

	#region Compliance Encryption Event ID Tests (90500-90599)

	[Fact]
	public void HaveEncryptionDecryptionServiceCreatedInComplianceRange()
	{
		PatternsEventId.EncryptionDecryptionServiceCreated.ShouldBe(90500);
	}

	[Fact]
	public void HaveAllComplianceEncryptionEventIdsInExpectedRange()
	{
		PatternsEventId.EncryptionDecryptionServiceCreated.ShouldBeInRange(90500, 90599);
		PatternsEventId.ReEncryptionServiceCreated.ShouldBeInRange(90500, 90599);
		PatternsEventId.ComplianceEncryptionApplied.ShouldBeInRange(90500, 90599);
		PatternsEventId.ComplianceDecryptionApplied.ShouldBeInRange(90500, 90599);
		PatternsEventId.ReEncryptionCompleted.ShouldBeInRange(90500, 90599);
	}

	#endregion

	#region Validation Services Event ID Tests (90600-90699)

	[Fact]
	public void HaveDefaultValidationServiceCreatedInValidationRange()
	{
		PatternsEventId.DefaultValidationServiceCreated.ShouldBe(90600);
	}

	[Fact]
	public void HaveAllValidationServicesEventIdsInExpectedRange()
	{
		PatternsEventId.DefaultValidationServiceCreated.ShouldBeInRange(90600, 90699);
		PatternsEventId.ProfileSpecificValidationExecuting.ShouldBeInRange(90600, 90699);
		PatternsEventId.ValidationRuleApplied.ShouldBeInRange(90600, 90699);
		PatternsEventId.ValidationPassed.ShouldBeInRange(90600, 90699);
		PatternsEventId.ValidationFailed.ShouldBeInRange(90600, 90699);
	}

	#endregion

	#region Context Validation Event ID Tests (90700-90799)

	[Fact]
	public void HaveContextValidationExecutingInContextValidationRange()
	{
		PatternsEventId.ContextValidationExecuting.ShouldBe(90700);
	}

	[Fact]
	public void HaveAllContextValidationEventIdsInExpectedRange()
	{
		PatternsEventId.ContextValidationExecuting.ShouldBeInRange(90700, 90799);
		PatternsEventId.DefaultContextValidatorCreated.ShouldBeInRange(90700, 90799);
		PatternsEventId.TraceContextValidatorCreated.ShouldBeInRange(90700, 90799);
		PatternsEventId.ContextValidationPassed.ShouldBeInRange(90700, 90799);
		PatternsEventId.ContextValidationFailed.ShouldBeInRange(90700, 90799);
	}

	#endregion

	#region Poison Message Core Event ID Tests (90800-90899)

	[Fact]
	public void HavePoisonMessageMiddlewareExecutingInPoisonMessageRange()
	{
		PatternsEventId.PoisonMessageMiddlewareExecuting.ShouldBe(90800);
	}

	[Fact]
	public void HaveAllPoisonMessageCoreEventIdsInExpectedRange()
	{
		PatternsEventId.PoisonMessageMiddlewareExecuting.ShouldBeInRange(90800, 90899);
		PatternsEventId.PoisonMessageDetected.ShouldBeInRange(90800, 90899);
		PatternsEventId.PoisonMessageHandlerExecuting.ShouldBeInRange(90800, 90899);
		PatternsEventId.PoisonMessageHandled.ShouldBeInRange(90800, 90899);
		PatternsEventId.MessageQuarantined.ShouldBeInRange(90800, 90899);
	}

	#endregion

	#region Poison Message Storage Event ID Tests (90900-90999)

	[Fact]
	public void HaveInMemoryDeadLetterStoreCreatedInPoisonStorageRange()
	{
		PatternsEventId.InMemoryDeadLetterStoreCreated.ShouldBe(90900);
	}

	[Fact]
	public void HaveAllPoisonMessageStorageEventIdsInExpectedRange()
	{
		PatternsEventId.InMemoryDeadLetterStoreCreated.ShouldBeInRange(90900, 90999);
		PatternsEventId.PoisonMessageCleanupServiceCreated.ShouldBeInRange(90900, 90999);
		PatternsEventId.CompositePoisonDetectorCreated.ShouldBeInRange(90900, 90999);
		PatternsEventId.DeadLetterMessageStored.ShouldBeInRange(90900, 90999);
		PatternsEventId.DeadLetterMessageRetrieved.ShouldBeInRange(90900, 90999);
		PatternsEventId.DeadLetterCleanupCompleted.ShouldBeInRange(90900, 90999);
	}

	#endregion

	#region Pool Configuration Event ID Tests (91000-91099)

	[Fact]
	public void HavePoolConfigurationAppliedInPoolConfigRange()
	{
		PatternsEventId.PoolConfigurationApplied.ShouldBe(91000);
	}

	[Fact]
	public void HaveAllPoolConfigurationEventIdsInExpectedRange()
	{
		PatternsEventId.PoolConfigurationApplied.ShouldBeInRange(91000, 91099);
		PatternsEventId.PoolServiceCollectionExtended.ShouldBeInRange(91000, 91099);
		PatternsEventId.PoolSizeConfigured.ShouldBeInRange(91000, 91099);
		PatternsEventId.PoolTimeoutConfigured.ShouldBeInRange(91000, 91099);
	}

	#endregion

	#region Performance Event ID Tests (91100-91199)

	[Fact]
	public void HavePerformanceBenchmarkStartedInPerformanceRange()
	{
		PatternsEventId.PerformanceBenchmarkStarted.ShouldBe(91100);
	}

	[Fact]
	public void HaveAllPerformanceEventIdsInExpectedRange()
	{
		PatternsEventId.PerformanceBenchmarkStarted.ShouldBeInRange(91100, 91199);
		PatternsEventId.PerformanceBenchmarkCompleted.ShouldBeInRange(91100, 91199);
		PatternsEventId.PerformanceMetricsCollected.ShouldBeInRange(91100, 91199);
		PatternsEventId.PerformanceThresholdExceeded.ShouldBeInRange(91100, 91199);
	}

	#endregion

	#region Azure Blob ClaimCheck Event ID Tests (91200-91299)

	[Fact]
	public void HaveAzureBlobPayloadStoredInAzureBlobRange()
	{
		PatternsEventId.AzureBlobPayloadStored.ShouldBe(91200);
	}

	[Fact]
	public void HaveAllAzureBlobEventIdsInExpectedRange()
	{
		PatternsEventId.AzureBlobPayloadStored.ShouldBeInRange(91200, 91299);
		PatternsEventId.AzureBlobPayloadRetrieved.ShouldBeInRange(91200, 91299);
		PatternsEventId.AzureBlobClaimCheckNotFound.ShouldBeInRange(91200, 91299);
		PatternsEventId.AzureBlobClaimCheckDeleted.ShouldBeInRange(91200, 91299);
		PatternsEventId.AzureBlobClaimCheckDeleteError.ShouldBeInRange(91200, 91299);
	}

	#endregion

	#region Patterns Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInPatternsReservedRange()
	{
		// Patterns reserved range is 90000-91999
		var allEventIds = GetAllPatternsEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(90000, 91999,
				$"Event ID {eventId} is outside Patterns reserved range (90000-91999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllPatternsEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllPatternsEventIds();
		allEventIds.Length.ShouldBeGreaterThan(70);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllPatternsEventIds()
	{
		return
		[
			// ClaimCheck Core (90000-90099)
			PatternsEventId.ClaimCheckCleanupServiceCreated,
			PatternsEventId.ClaimCheckCleanupStarted,
			PatternsEventId.ClaimCheckCleanupCompleted,
			PatternsEventId.ClaimStored,
			PatternsEventId.ClaimRetrieved,
			PatternsEventId.ClaimDeleted,
			PatternsEventId.ClaimExpired,
			PatternsEventId.ClaimCheckCleanupDisabled,
			PatternsEventId.ClaimCheckCleanupStartedInterval,
			PatternsEventId.ClaimCheckCleanupTaskRunning,
			PatternsEventId.ClaimCheckCleanupError,
			PatternsEventId.ClaimCheckCleanupServiceStopped,

			// ClaimCheck InMemory (90100-90199)
			PatternsEventId.InMemoryClaimCheckCleanupCreated,
			PatternsEventId.InMemoryClaimStored,
			PatternsEventId.InMemoryClaimRetrieved,
			PatternsEventId.InMemoryCleanupCompleted,
			PatternsEventId.InMemoryCleanupDisabled,
			PatternsEventId.InMemoryCleanupStartedInterval,
			PatternsEventId.InMemoryCleanupServiceStopped,
			PatternsEventId.InMemoryCleanupTaskRunning,
			PatternsEventId.InMemoryCleanupError,
			PatternsEventId.InMemoryExpiredClaimsRemoved,

			// Routing Patterns (90200-90299)
			PatternsEventId.RoutingPolicyEvaluatorCreated,
			PatternsEventId.RoutingPolicyEvaluated,
			PatternsEventId.RouteSelected,
			PatternsEventId.TimeZoneAwareRouterCreated,
			PatternsEventId.TimeZoneAwareRoutingApplied,
			PatternsEventId.NoRouteMatched,
			PatternsEventId.RoutingRulesEvaluated,
			PatternsEventId.RuleNotApplicableTimeRange,
			PatternsEventId.RuleNotApplicableDayOfWeek,
			PatternsEventId.RuleNotApplicableDateRange,
			PatternsEventId.RuleNotApplicableConditions,
			PatternsEventId.RuleIsApplicable,
			PatternsEventId.TimezoneNotFoundLocalTime,
			PatternsEventId.TimezoneNotFoundUtc,
			PatternsEventId.RouteInBusinessHours,
			PatternsEventId.RouteOutsideBusinessHours,
			PatternsEventId.NoRoutesInBusinessHoursFallback,

			// Load Balancing (90300-90399)
			PatternsEventId.WeightedRoundRobinCreated,
			PatternsEventId.LeastConnectionsCreated,
			PatternsEventId.ConsistentHashCreated,
			PatternsEventId.RandomLoadBalancerCreated,
			PatternsEventId.EndpointSelected,
			PatternsEventId.EndpointUnavailable,
			PatternsEventId.LoadBalancerRebalancing,

			// Route Health (90400-90499)
			PatternsEventId.RouteHealthMonitorCreated,
			PatternsEventId.RouteHealthCheckPerformed,
			PatternsEventId.RouteMarkedHealthy,
			PatternsEventId.RouteMarkedUnhealthy,
			PatternsEventId.RouteHealthStatusChanged,

			// Compliance Encryption (90500-90599)
			PatternsEventId.EncryptionDecryptionServiceCreated,
			PatternsEventId.ReEncryptionServiceCreated,
			PatternsEventId.ComplianceEncryptionApplied,
			PatternsEventId.ComplianceDecryptionApplied,
			PatternsEventId.ReEncryptionCompleted,

			// Validation Services (90600-90699)
			PatternsEventId.DefaultValidationServiceCreated,
			PatternsEventId.ProfileSpecificValidationExecuting,
			PatternsEventId.ValidationRuleApplied,
			PatternsEventId.ValidationPassed,
			PatternsEventId.ValidationFailed,

			// Context Validation (90700-90799)
			PatternsEventId.ContextValidationExecuting,
			PatternsEventId.DefaultContextValidatorCreated,
			PatternsEventId.TraceContextValidatorCreated,
			PatternsEventId.ContextValidationPassed,
			PatternsEventId.ContextValidationFailed,

			// Poison Message Core (90800-90899)
			PatternsEventId.PoisonMessageMiddlewareExecuting,
			PatternsEventId.PoisonMessageDetected,
			PatternsEventId.PoisonMessageHandlerExecuting,
			PatternsEventId.PoisonMessageHandled,
			PatternsEventId.MessageQuarantined,

			// Poison Message Storage (90900-90999)
			PatternsEventId.InMemoryDeadLetterStoreCreated,
			PatternsEventId.PoisonMessageCleanupServiceCreated,
			PatternsEventId.CompositePoisonDetectorCreated,
			PatternsEventId.DeadLetterMessageStored,
			PatternsEventId.DeadLetterMessageRetrieved,
			PatternsEventId.DeadLetterCleanupCompleted,

			// Pool Configuration (91000-91099)
			PatternsEventId.PoolConfigurationApplied,
			PatternsEventId.PoolServiceCollectionExtended,
			PatternsEventId.PoolSizeConfigured,
			PatternsEventId.PoolTimeoutConfigured,

			// Performance (91100-91199)
			PatternsEventId.PerformanceBenchmarkStarted,
			PatternsEventId.PerformanceBenchmarkCompleted,
			PatternsEventId.PerformanceMetricsCollected,
			PatternsEventId.PerformanceThresholdExceeded,

			// Azure Blob ClaimCheck (91200-91299)
			PatternsEventId.AzureBlobPayloadStored,
			PatternsEventId.AzureBlobPayloadRetrieved,
			PatternsEventId.AzureBlobClaimCheckNotFound,
			PatternsEventId.AzureBlobClaimCheckDeleted,
			PatternsEventId.AzureBlobClaimCheckDeleteError
		];
	}

	#endregion
}
