// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.Diagnostics;

namespace Excalibur.Dispatch.Patterns.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="PatternsEventId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "Diagnostics")]
public sealed class PatternsEventIdShould
{
	// ========================================
	// ClaimCheck Core (90000-90099)
	// ========================================

	[Fact]
	public void HaveClaimCheckCleanupServiceCreatedInRange()
	{
		PatternsEventId.ClaimCheckCleanupServiceCreated.ShouldBe(90000);
	}

	[Fact]
	public void HaveClaimCheckCleanupStartedInRange()
	{
		PatternsEventId.ClaimCheckCleanupStarted.ShouldBe(90001);
	}

	[Fact]
	public void HaveClaimCheckCleanupCompletedInRange()
	{
		PatternsEventId.ClaimCheckCleanupCompleted.ShouldBe(90002);
	}

	[Fact]
	public void HaveClaimStoredInRange()
	{
		PatternsEventId.ClaimStored.ShouldBe(90003);
	}

	[Fact]
	public void HaveClaimRetrievedInRange()
	{
		PatternsEventId.ClaimRetrieved.ShouldBe(90004);
	}

	[Fact]
	public void HaveClaimDeletedInRange()
	{
		PatternsEventId.ClaimDeleted.ShouldBe(90005);
	}

	[Fact]
	public void HaveClaimExpiredInRange()
	{
		PatternsEventId.ClaimExpired.ShouldBe(90006);
	}

	[Fact]
	public void HaveClaimCheckCleanupDisabledInRange()
	{
		PatternsEventId.ClaimCheckCleanupDisabled.ShouldBe(90007);
	}

	[Fact]
	public void HaveClaimCheckCleanupStartedIntervalInRange()
	{
		PatternsEventId.ClaimCheckCleanupStartedInterval.ShouldBe(90008);
	}

	[Fact]
	public void HaveClaimCheckCleanupTaskRunningInRange()
	{
		PatternsEventId.ClaimCheckCleanupTaskRunning.ShouldBe(90009);
	}

	[Fact]
	public void HaveClaimCheckCleanupErrorInRange()
	{
		PatternsEventId.ClaimCheckCleanupError.ShouldBe(90010);
	}

	[Fact]
	public void HaveClaimCheckCleanupServiceStoppedInRange()
	{
		PatternsEventId.ClaimCheckCleanupServiceStopped.ShouldBe(90011);
	}

	// ========================================
	// ClaimCheck InMemory (90100-90199)
	// ========================================

	[Fact]
	public void HaveInMemoryClaimCheckCleanupCreatedInRange()
	{
		PatternsEventId.InMemoryClaimCheckCleanupCreated.ShouldBe(90100);
	}

	[Fact]
	public void HaveInMemoryClaimStoredInRange()
	{
		PatternsEventId.InMemoryClaimStored.ShouldBe(90101);
	}

	[Fact]
	public void HaveInMemoryClaimRetrievedInRange()
	{
		PatternsEventId.InMemoryClaimRetrieved.ShouldBe(90102);
	}

	[Fact]
	public void HaveInMemoryCleanupCompletedInRange()
	{
		PatternsEventId.InMemoryCleanupCompleted.ShouldBe(90103);
	}

	[Fact]
	public void HaveInMemoryCleanupDisabledInRange()
	{
		PatternsEventId.InMemoryCleanupDisabled.ShouldBe(90104);
	}

	[Fact]
	public void HaveInMemoryCleanupStartedIntervalInRange()
	{
		PatternsEventId.InMemoryCleanupStartedInterval.ShouldBe(90105);
	}

	[Fact]
	public void HaveInMemoryCleanupServiceStoppedInRange()
	{
		PatternsEventId.InMemoryCleanupServiceStopped.ShouldBe(90106);
	}

	[Fact]
	public void HaveInMemoryCleanupTaskRunningInRange()
	{
		PatternsEventId.InMemoryCleanupTaskRunning.ShouldBe(90107);
	}

	[Fact]
	public void HaveInMemoryCleanupErrorInRange()
	{
		PatternsEventId.InMemoryCleanupError.ShouldBe(90108);
	}

	[Fact]
	public void HaveInMemoryExpiredClaimsRemovedInRange()
	{
		PatternsEventId.InMemoryExpiredClaimsRemoved.ShouldBe(90109);
	}

	// ========================================
	// Routing Patterns (90200-90299)
	// ========================================

	[Fact]
	public void HaveRoutingPolicyEvaluatorCreatedInRange()
	{
		PatternsEventId.RoutingPolicyEvaluatorCreated.ShouldBe(90200);
	}

	[Fact]
	public void HaveRoutingPolicyEvaluatedInRange()
	{
		PatternsEventId.RoutingPolicyEvaluated.ShouldBe(90201);
	}

	[Fact]
	public void HaveRouteSelectedInRange()
	{
		PatternsEventId.RouteSelected.ShouldBe(90202);
	}

	[Fact]
	public void HaveTimeZoneAwareRouterCreatedInRange()
	{
		PatternsEventId.TimeZoneAwareRouterCreated.ShouldBe(90203);
	}

	[Fact]
	public void HaveTimeZoneAwareRoutingAppliedInRange()
	{
		PatternsEventId.TimeZoneAwareRoutingApplied.ShouldBe(90204);
	}

	[Fact]
	public void HaveNoRouteMatchedInRange()
	{
		PatternsEventId.NoRouteMatched.ShouldBe(90205);
	}

	[Fact]
	public void HaveRoutingRulesEvaluatedInRange()
	{
		PatternsEventId.RoutingRulesEvaluated.ShouldBe(90206);
	}

	[Fact]
	public void HaveRuleNotApplicableTimeRangeInRange()
	{
		PatternsEventId.RuleNotApplicableTimeRange.ShouldBe(90207);
	}

	[Fact]
	public void HaveRuleNotApplicableDayOfWeekInRange()
	{
		PatternsEventId.RuleNotApplicableDayOfWeek.ShouldBe(90208);
	}

	[Fact]
	public void HaveRuleNotApplicableDateRangeInRange()
	{
		PatternsEventId.RuleNotApplicableDateRange.ShouldBe(90209);
	}

	[Fact]
	public void HaveRuleNotApplicableConditionsInRange()
	{
		PatternsEventId.RuleNotApplicableConditions.ShouldBe(90210);
	}

	[Fact]
	public void HaveRuleIsApplicableInRange()
	{
		PatternsEventId.RuleIsApplicable.ShouldBe(90211);
	}

	[Fact]
	public void HaveTimezoneNotFoundLocalTimeInRange()
	{
		PatternsEventId.TimezoneNotFoundLocalTime.ShouldBe(90212);
	}

	[Fact]
	public void HaveTimezoneNotFoundUtcInRange()
	{
		PatternsEventId.TimezoneNotFoundUtc.ShouldBe(90213);
	}

	[Fact]
	public void HaveRouteInBusinessHoursInRange()
	{
		PatternsEventId.RouteInBusinessHours.ShouldBe(90214);
	}

	[Fact]
	public void HaveRouteOutsideBusinessHoursInRange()
	{
		PatternsEventId.RouteOutsideBusinessHours.ShouldBe(90215);
	}

	[Fact]
	public void HaveNoRoutesInBusinessHoursFallbackInRange()
	{
		PatternsEventId.NoRoutesInBusinessHoursFallback.ShouldBe(90216);
	}

	// ========================================
	// Load Balancing (90300-90399)
	// ========================================

	[Fact]
	public void HaveWeightedRoundRobinCreatedInRange()
	{
		PatternsEventId.WeightedRoundRobinCreated.ShouldBe(90300);
	}

	[Fact]
	public void HaveLeastConnectionsCreatedInRange()
	{
		PatternsEventId.LeastConnectionsCreated.ShouldBe(90301);
	}

	[Fact]
	public void HaveConsistentHashCreatedInRange()
	{
		PatternsEventId.ConsistentHashCreated.ShouldBe(90302);
	}

	[Fact]
	public void HaveRandomLoadBalancerCreatedInRange()
	{
		PatternsEventId.RandomLoadBalancerCreated.ShouldBe(90303);
	}

	[Fact]
	public void HaveEndpointSelectedInRange()
	{
		PatternsEventId.EndpointSelected.ShouldBe(90304);
	}

	[Fact]
	public void HaveEndpointUnavailableInRange()
	{
		PatternsEventId.EndpointUnavailable.ShouldBe(90305);
	}

	[Fact]
	public void HaveLoadBalancerRebalancingInRange()
	{
		PatternsEventId.LoadBalancerRebalancing.ShouldBe(90306);
	}

	// ========================================
	// Route Health (90400-90499)
	// ========================================

	[Fact]
	public void HaveRouteHealthMonitorCreatedInRange()
	{
		PatternsEventId.RouteHealthMonitorCreated.ShouldBe(90400);
	}

	[Fact]
	public void HaveRouteHealthCheckPerformedInRange()
	{
		PatternsEventId.RouteHealthCheckPerformed.ShouldBe(90401);
	}

	[Fact]
	public void HaveRouteMarkedHealthyInRange()
	{
		PatternsEventId.RouteMarkedHealthy.ShouldBe(90402);
	}

	[Fact]
	public void HaveRouteMarkedUnhealthyInRange()
	{
		PatternsEventId.RouteMarkedUnhealthy.ShouldBe(90403);
	}

	[Fact]
	public void HaveRouteHealthStatusChangedInRange()
	{
		PatternsEventId.RouteHealthStatusChanged.ShouldBe(90404);
	}

	// ========================================
	// Compliance Encryption (90500-90599)
	// ========================================

	[Fact]
	public void HaveEncryptionDecryptionServiceCreatedInRange()
	{
		PatternsEventId.EncryptionDecryptionServiceCreated.ShouldBe(90500);
	}

	[Fact]
	public void HaveReEncryptionServiceCreatedInRange()
	{
		PatternsEventId.ReEncryptionServiceCreated.ShouldBe(90501);
	}

	[Fact]
	public void HaveComplianceEncryptionAppliedInRange()
	{
		PatternsEventId.ComplianceEncryptionApplied.ShouldBe(90502);
	}

	[Fact]
	public void HaveComplianceDecryptionAppliedInRange()
	{
		PatternsEventId.ComplianceDecryptionApplied.ShouldBe(90503);
	}

	[Fact]
	public void HaveReEncryptionCompletedInRange()
	{
		PatternsEventId.ReEncryptionCompleted.ShouldBe(90504);
	}

	// ========================================
	// Validation Services (90600-90699)
	// ========================================

	[Fact]
	public void HaveDefaultValidationServiceCreatedInRange()
	{
		PatternsEventId.DefaultValidationServiceCreated.ShouldBe(90600);
	}

	[Fact]
	public void HaveProfileSpecificValidationExecutingInRange()
	{
		PatternsEventId.ProfileSpecificValidationExecuting.ShouldBe(90601);
	}

	[Fact]
	public void HaveValidationRuleAppliedInRange()
	{
		PatternsEventId.ValidationRuleApplied.ShouldBe(90602);
	}

	[Fact]
	public void HaveValidationPassedInRange()
	{
		PatternsEventId.ValidationPassed.ShouldBe(90603);
	}

	[Fact]
	public void HaveValidationFailedInRange()
	{
		PatternsEventId.ValidationFailed.ShouldBe(90604);
	}

	// ========================================
	// Context Validation (90700-90799)
	// ========================================

	[Fact]
	public void HaveContextValidationExecutingInRange()
	{
		PatternsEventId.ContextValidationExecuting.ShouldBe(90700);
	}

	[Fact]
	public void HaveDefaultContextValidatorCreatedInRange()
	{
		PatternsEventId.DefaultContextValidatorCreated.ShouldBe(90701);
	}

	[Fact]
	public void HaveTraceContextValidatorCreatedInRange()
	{
		PatternsEventId.TraceContextValidatorCreated.ShouldBe(90702);
	}

	[Fact]
	public void HaveContextValidationPassedInRange()
	{
		PatternsEventId.ContextValidationPassed.ShouldBe(90703);
	}

	[Fact]
	public void HaveContextValidationFailedInRange()
	{
		PatternsEventId.ContextValidationFailed.ShouldBe(90704);
	}

	// ========================================
	// Poison Message Core (90800-90899)
	// ========================================

	[Fact]
	public void HavePoisonMessageMiddlewareExecutingInRange()
	{
		PatternsEventId.PoisonMessageMiddlewareExecuting.ShouldBe(90800);
	}

	[Fact]
	public void HavePoisonMessageDetectedInRange()
	{
		PatternsEventId.PoisonMessageDetected.ShouldBe(90801);
	}

	[Fact]
	public void HavePoisonMessageHandlerExecutingInRange()
	{
		PatternsEventId.PoisonMessageHandlerExecuting.ShouldBe(90802);
	}

	[Fact]
	public void HavePoisonMessageHandledInRange()
	{
		PatternsEventId.PoisonMessageHandled.ShouldBe(90803);
	}

	[Fact]
	public void HaveMessageQuarantinedInRange()
	{
		PatternsEventId.MessageQuarantined.ShouldBe(90804);
	}

	// ========================================
	// Poison Message Storage (90900-90999)
	// ========================================

	[Fact]
	public void HaveInMemoryDeadLetterStoreCreatedInRange()
	{
		PatternsEventId.InMemoryDeadLetterStoreCreated.ShouldBe(90900);
	}

	[Fact]
	public void HavePoisonMessageCleanupServiceCreatedInRange()
	{
		PatternsEventId.PoisonMessageCleanupServiceCreated.ShouldBe(90901);
	}

	[Fact]
	public void HaveCompositePoisonDetectorCreatedInRange()
	{
		PatternsEventId.CompositePoisonDetectorCreated.ShouldBe(90902);
	}

	[Fact]
	public void HaveDeadLetterMessageStoredInRange()
	{
		PatternsEventId.DeadLetterMessageStored.ShouldBe(90903);
	}

	[Fact]
	public void HaveDeadLetterMessageRetrievedInRange()
	{
		PatternsEventId.DeadLetterMessageRetrieved.ShouldBe(90904);
	}

	[Fact]
	public void HaveDeadLetterCleanupCompletedInRange()
	{
		PatternsEventId.DeadLetterCleanupCompleted.ShouldBe(90905);
	}

	// ========================================
	// Pool Configuration (91000-91099)
	// ========================================

	[Fact]
	public void HavePoolConfigurationAppliedInRange()
	{
		PatternsEventId.PoolConfigurationApplied.ShouldBe(91000);
	}

	[Fact]
	public void HavePoolServiceCollectionExtendedInRange()
	{
		PatternsEventId.PoolServiceCollectionExtended.ShouldBe(91001);
	}

	[Fact]
	public void HavePoolSizeConfiguredInRange()
	{
		PatternsEventId.PoolSizeConfigured.ShouldBe(91002);
	}

	[Fact]
	public void HavePoolTimeoutConfiguredInRange()
	{
		PatternsEventId.PoolTimeoutConfigured.ShouldBe(91003);
	}

	// ========================================
	// Performance (91100-91199)
	// ========================================

	[Fact]
	public void HavePerformanceBenchmarkStartedInRange()
	{
		PatternsEventId.PerformanceBenchmarkStarted.ShouldBe(91100);
	}

	[Fact]
	public void HavePerformanceBenchmarkCompletedInRange()
	{
		PatternsEventId.PerformanceBenchmarkCompleted.ShouldBe(91101);
	}

	[Fact]
	public void HavePerformanceMetricsCollectedInRange()
	{
		PatternsEventId.PerformanceMetricsCollected.ShouldBe(91102);
	}

	[Fact]
	public void HavePerformanceThresholdExceededInRange()
	{
		PatternsEventId.PerformanceThresholdExceeded.ShouldBe(91103);
	}

	// ========================================
	// Azure Blob ClaimCheck Provider (91200-91299)
	// ========================================

	[Fact]
	public void HaveAzureBlobPayloadStoredInRange()
	{
		PatternsEventId.AzureBlobPayloadStored.ShouldBe(91200);
	}

	[Fact]
	public void HaveAzureBlobPayloadRetrievedInRange()
	{
		PatternsEventId.AzureBlobPayloadRetrieved.ShouldBe(91201);
	}

	[Fact]
	public void HaveAzureBlobClaimCheckNotFoundInRange()
	{
		PatternsEventId.AzureBlobClaimCheckNotFound.ShouldBe(91202);
	}

	[Fact]
	public void HaveAzureBlobClaimCheckDeletedInRange()
	{
		PatternsEventId.AzureBlobClaimCheckDeleted.ShouldBe(91203);
	}

	[Fact]
	public void HaveAzureBlobClaimCheckDeleteErrorInRange()
	{
		PatternsEventId.AzureBlobClaimCheckDeleteError.ShouldBe(91204);
	}

	// ========================================
	// Range Verification Tests
	// ========================================

	[Theory]
	[InlineData(90000, 90099)]
	[InlineData(90100, 90199)]
	[InlineData(90200, 90299)]
	[InlineData(90300, 90399)]
	[InlineData(90400, 90499)]
	[InlineData(90500, 90599)]
	[InlineData(90600, 90699)]
	[InlineData(90700, 90799)]
	[InlineData(90800, 90899)]
	[InlineData(90900, 90999)]
	[InlineData(91000, 91099)]
	[InlineData(91100, 91199)]
	[InlineData(91200, 91299)]
	public void HaveEventIdRangesWithinPatternsRange(int rangeStart, int rangeEnd)
	{
		// All ranges should be within 90000-91999
		rangeStart.ShouldBeGreaterThanOrEqualTo(90000);
		rangeEnd.ShouldBeLessThanOrEqualTo(91999);
	}
}
