// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Diagnostics;

namespace Excalibur.Data.Tests.ElasticSearch;

/// <summary>
/// Unit tests for <see cref="DataElasticsearchEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.Elasticsearch")]
[Trait("Priority", "0")]
public sealed class DataElasticsearchEventIdShould : UnitTestBase
{
	#region Client Management Event ID Tests (106000-106099)

	[Fact]
	public void HaveClientCreatedInClientManagementRange()
	{
		DataElasticsearchEventId.ClientCreated.ShouldBe(106000);
	}

	[Fact]
	public void HaveAllClientManagementEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.ClientCreated.ShouldBeInRange(106000, 106099);
		DataElasticsearchEventId.ClientDisposed.ShouldBeInRange(106000, 106099);
		DataElasticsearchEventId.ClusterHealthChecked.ShouldBeInRange(106000, 106099);
		DataElasticsearchEventId.NodeDiscovered.ShouldBeInRange(106000, 106099);
		DataElasticsearchEventId.ConnectionPoolConfigured.ShouldBeInRange(106000, 106099);
	}

	#endregion

	#region Index Operations Event ID Tests (106100-106105)

	[Fact]
	public void HaveIndexCreatedInIndexOperationsRange()
	{
		DataElasticsearchEventId.IndexCreated.ShouldBe(106100);
	}

	[Fact]
	public void HaveAllIndexOperationsEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.IndexCreated.ShouldBeInRange(106100, 106105);
		DataElasticsearchEventId.IndexDeleted.ShouldBeInRange(106100, 106105);
		DataElasticsearchEventId.IndexExistsChecked.ShouldBeInRange(106100, 106105);
		DataElasticsearchEventId.IndexMappingUpdated.ShouldBeInRange(106100, 106105);
		DataElasticsearchEventId.IndexSettingsUpdated.ShouldBeInRange(106100, 106105);
		DataElasticsearchEventId.IndexAliasCreated.ShouldBeInRange(106100, 106105);
	}

	#endregion

	#region Index Lifecycle Management Event ID Tests (106106-106149)

	[Fact]
	public void HaveCreatingLifecyclePolicyInIlmRange()
	{
		DataElasticsearchEventId.CreatingLifecyclePolicy.ShouldBe(106106);
	}

	[Fact]
	public void HaveAllIlmEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.CreatingLifecyclePolicy.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyCreated.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyCreationFailed.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyCreationException.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.DeletingLifecyclePolicy.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyDeleted.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyNotFound.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyDeletionFailed.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecyclePolicyDeletionException.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.RollingOverIndex.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexRolledOver.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexRolloverFailed.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexRolloverException.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.GettingLifecycleStatus.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecycleStatusRetrieved.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecycleStatusFailed.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.LifecycleStatusException.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.MovingToNextPhase.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.NoIndicesFoundForPhaseMove.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexHasNoPolicy.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexAlreadyInFinalPhase.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexMovedToNextPhase.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.IndexMoveToNextPhaseFailed.ShouldBeInRange(106106, 106149);
		DataElasticsearchEventId.MoveToNextPhaseException.ShouldBeInRange(106106, 106149);
	}

	#endregion

	#region Document Operations Event ID Tests (106200-106299)

	[Fact]
	public void HaveDocumentIndexedInDocumentOperationsRange()
	{
		DataElasticsearchEventId.DocumentIndexed.ShouldBe(106200);
	}

	[Fact]
	public void HaveAllDocumentOperationsEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.DocumentIndexed.ShouldBeInRange(106200, 106299);
		DataElasticsearchEventId.DocumentRetrieved.ShouldBeInRange(106200, 106299);
		DataElasticsearchEventId.DocumentUpdated.ShouldBeInRange(106200, 106299);
		DataElasticsearchEventId.DocumentDeleted.ShouldBeInRange(106200, 106299);
		DataElasticsearchEventId.DocumentSourceRetrieved.ShouldBeInRange(106200, 106299);
		DataElasticsearchEventId.DocumentExistsChecked.ShouldBeInRange(106200, 106299);
	}

	#endregion

	#region Search/Query Event ID Tests (106300-106399)

	[Fact]
	public void HaveSearchExecutingInSearchQueryRange()
	{
		DataElasticsearchEventId.SearchExecuting.ShouldBe(106300);
	}

	[Fact]
	public void HaveAllSearchQueryEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.SearchExecuting.ShouldBeInRange(106300, 106399);
		DataElasticsearchEventId.SearchExecuted.ShouldBeInRange(106300, 106399);
		DataElasticsearchEventId.MultiSearchExecuted.ShouldBeInRange(106300, 106399);
		DataElasticsearchEventId.ScrollSearchStarted.ShouldBeInRange(106300, 106399);
		DataElasticsearchEventId.ScrollSearchContinued.ShouldBeInRange(106300, 106399);
		DataElasticsearchEventId.ScrollSearchCleared.ShouldBeInRange(106300, 106399);
	}

	#endregion

	#region Aggregations Event ID Tests (106400-106499)

	[Fact]
	public void HaveAggregationExecutedInAggregationsRange()
	{
		DataElasticsearchEventId.AggregationExecuted.ShouldBe(106400);
	}

	[Fact]
	public void HaveAllAggregationsEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.AggregationExecuted.ShouldBeInRange(106400, 106499);
		DataElasticsearchEventId.TermsAggregationExecuted.ShouldBeInRange(106400, 106499);
		DataElasticsearchEventId.DateHistogramExecuted.ShouldBeInRange(106400, 106499);
		DataElasticsearchEventId.NestedAggregationExecuted.ShouldBeInRange(106400, 106499);
	}

	#endregion

	#region Bulk Operations Event ID Tests (106500-106599)

	[Fact]
	public void HaveBulkOperationStartedInBulkOperationsRange()
	{
		DataElasticsearchEventId.BulkOperationStarted.ShouldBe(106500);
	}

	[Fact]
	public void HaveAllBulkOperationsEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.BulkOperationStarted.ShouldBeInRange(106500, 106599);
		DataElasticsearchEventId.BulkOperationCompleted.ShouldBeInRange(106500, 106599);
		DataElasticsearchEventId.BulkItemSucceeded.ShouldBeInRange(106500, 106599);
		DataElasticsearchEventId.BulkItemFailed.ShouldBeInRange(106500, 106599);
		DataElasticsearchEventId.BulkAllHelperExecuting.ShouldBeInRange(106500, 106599);
	}

	#endregion

	#region Error Handling Event ID Tests (106600-106699)

	[Fact]
	public void HaveIndexNotFoundInErrorHandlingRange()
	{
		DataElasticsearchEventId.IndexNotFound.ShouldBe(106600);
	}

	[Fact]
	public void HaveAllErrorHandlingEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.IndexNotFound.ShouldBeInRange(106600, 106699);
		DataElasticsearchEventId.DocumentNotFound.ShouldBeInRange(106600, 106699);
		DataElasticsearchEventId.VersionConflict.ShouldBeInRange(106600, 106699);
		DataElasticsearchEventId.ElasticsearchException.ShouldBeInRange(106600, 106699);
		DataElasticsearchEventId.RequestTimeout.ShouldBeInRange(106600, 106699);
		DataElasticsearchEventId.CircuitBreakerTriggered.ShouldBeInRange(106600, 106699);
	}

	#endregion

	#region Security Monitoring Event ID Tests (106700-106799)

	[Fact]
	public void HaveSecurityMonitoringStartedInSecurityMonitoringRange()
	{
		DataElasticsearchEventId.SecurityMonitoringStarted.ShouldBe(106700);
	}

	[Fact]
	public void HaveAllSecurityMonitoringEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.SecurityMonitoringStarted.ShouldBeInRange(106700, 106799);
		DataElasticsearchEventId.SecurityMonitoringStopped.ShouldBeInRange(106700, 106799);
	}

	#endregion

	#region Performance/Repository Event ID Tests (106800-106899)

	[Fact]
	public void HaveRepositorySlowQueryInPerformanceRange()
	{
		DataElasticsearchEventId.RepositorySlowQuery.ShouldBe(106800);
	}

	[Fact]
	public void HaveAllPerformanceEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.RepositorySlowQuery.ShouldBeInRange(106800, 106899);
		DataElasticsearchEventId.CacheWarmingError.ShouldBeInRange(106800, 106899);
	}

	#endregion

	#region Query Optimization Event ID Tests (106900-106909)

	[Fact]
	public void HaveSearchRequestOptimizationFailedInQueryOptimizationRange()
	{
		DataElasticsearchEventId.SearchRequestOptimizationFailed.ShouldBe(106900);
	}

	[Fact]
	public void HaveAllQueryOptimizationEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.SearchRequestOptimizationFailed.ShouldBeInRange(106900, 106909);
		DataElasticsearchEventId.QueryExecutionAnalysisFailed.ShouldBeInRange(106900, 106909);
		DataElasticsearchEventId.SlowQueryDetected.ShouldBeInRange(106900, 106909);
	}

	#endregion

	#region Projection Store Event ID Tests (106910-106919)

	[Fact]
	public void HaveProjectionStoreInitializedInProjectionStoreRange()
	{
		DataElasticsearchEventId.ProjectionStoreInitialized.ShouldBe(106910);
	}

	[Fact]
	public void HaveAllProjectionStoreEventIdsInExpectedRange()
	{
		DataElasticsearchEventId.ProjectionStoreInitialized.ShouldBeInRange(106910, 106919);
		DataElasticsearchEventId.ProjectionUpserted.ShouldBeInRange(106910, 106919);
		DataElasticsearchEventId.ProjectionDeleted.ShouldBeInRange(106910, 106919);
		DataElasticsearchEventId.ProjectionIndexCreationFailed.ShouldBeInRange(106910, 106919);
	}

	#endregion

	#region Elasticsearch Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInElasticsearchReservedRange()
	{
		// Elasticsearch reserved range is 106000-106999
		var allEventIds = GetAllElasticsearchEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(106000, 106999,
				$"Event ID {eventId} is outside Elasticsearch reserved range (106000-106999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllElasticsearchEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllElasticsearchEventIds();
		allEventIds.Length.ShouldBeGreaterThan(60);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllElasticsearchEventIds()
	{
		return
		[
			// Client Management (106000-106099)
			DataElasticsearchEventId.ClientCreated,
			DataElasticsearchEventId.ClientDisposed,
			DataElasticsearchEventId.ClusterHealthChecked,
			DataElasticsearchEventId.NodeDiscovered,
			DataElasticsearchEventId.ConnectionPoolConfigured,

			// Index Operations (106100-106105)
			DataElasticsearchEventId.IndexCreated,
			DataElasticsearchEventId.IndexDeleted,
			DataElasticsearchEventId.IndexExistsChecked,
			DataElasticsearchEventId.IndexMappingUpdated,
			DataElasticsearchEventId.IndexSettingsUpdated,
			DataElasticsearchEventId.IndexAliasCreated,

			// Index Lifecycle Management (106106-106149)
			DataElasticsearchEventId.CreatingLifecyclePolicy,
			DataElasticsearchEventId.LifecyclePolicyCreated,
			DataElasticsearchEventId.LifecyclePolicyCreationFailed,
			DataElasticsearchEventId.LifecyclePolicyCreationException,
			DataElasticsearchEventId.DeletingLifecyclePolicy,
			DataElasticsearchEventId.LifecyclePolicyDeleted,
			DataElasticsearchEventId.LifecyclePolicyNotFound,
			DataElasticsearchEventId.LifecyclePolicyDeletionFailed,
			DataElasticsearchEventId.LifecyclePolicyDeletionException,
			DataElasticsearchEventId.RollingOverIndex,
			DataElasticsearchEventId.IndexRolledOver,
			DataElasticsearchEventId.IndexRolloverFailed,
			DataElasticsearchEventId.IndexRolloverException,
			DataElasticsearchEventId.GettingLifecycleStatus,
			DataElasticsearchEventId.LifecycleStatusRetrieved,
			DataElasticsearchEventId.LifecycleStatusFailed,
			DataElasticsearchEventId.LifecycleStatusException,
			DataElasticsearchEventId.MovingToNextPhase,
			DataElasticsearchEventId.NoIndicesFoundForPhaseMove,
			DataElasticsearchEventId.IndexHasNoPolicy,
			DataElasticsearchEventId.IndexAlreadyInFinalPhase,
			DataElasticsearchEventId.IndexMovedToNextPhase,
			DataElasticsearchEventId.IndexMoveToNextPhaseFailed,
			DataElasticsearchEventId.MoveToNextPhaseException,

			// Document Operations (106200-106299)
			DataElasticsearchEventId.DocumentIndexed,
			DataElasticsearchEventId.DocumentRetrieved,
			DataElasticsearchEventId.DocumentUpdated,
			DataElasticsearchEventId.DocumentDeleted,
			DataElasticsearchEventId.DocumentSourceRetrieved,
			DataElasticsearchEventId.DocumentExistsChecked,

			// Search/Query (106300-106399)
			DataElasticsearchEventId.SearchExecuting,
			DataElasticsearchEventId.SearchExecuted,
			DataElasticsearchEventId.MultiSearchExecuted,
			DataElasticsearchEventId.ScrollSearchStarted,
			DataElasticsearchEventId.ScrollSearchContinued,
			DataElasticsearchEventId.ScrollSearchCleared,

			// Aggregations (106400-106499)
			DataElasticsearchEventId.AggregationExecuted,
			DataElasticsearchEventId.TermsAggregationExecuted,
			DataElasticsearchEventId.DateHistogramExecuted,
			DataElasticsearchEventId.NestedAggregationExecuted,

			// Bulk Operations (106500-106599)
			DataElasticsearchEventId.BulkOperationStarted,
			DataElasticsearchEventId.BulkOperationCompleted,
			DataElasticsearchEventId.BulkItemSucceeded,
			DataElasticsearchEventId.BulkItemFailed,
			DataElasticsearchEventId.BulkAllHelperExecuting,

			// Error Handling (106600-106699)
			DataElasticsearchEventId.IndexNotFound,
			DataElasticsearchEventId.DocumentNotFound,
			DataElasticsearchEventId.VersionConflict,
			DataElasticsearchEventId.ElasticsearchException,
			DataElasticsearchEventId.RequestTimeout,
			DataElasticsearchEventId.CircuitBreakerTriggered,

			// Security Monitoring (106700-106799)
			DataElasticsearchEventId.SecurityMonitoringStarted,
			DataElasticsearchEventId.SecurityMonitoringStopped,

			// Performance/Repository (106800-106899)
			DataElasticsearchEventId.RepositorySlowQuery,
			DataElasticsearchEventId.CacheWarmingError,

			// Query Optimization (106900-106909)
			DataElasticsearchEventId.SearchRequestOptimizationFailed,
			DataElasticsearchEventId.QueryExecutionAnalysisFailed,
			DataElasticsearchEventId.SlowQueryDetected,

			// Projection Store (106910-106919)
			DataElasticsearchEventId.ProjectionStoreInitialized,
			DataElasticsearchEventId.ProjectionUpserted,
			DataElasticsearchEventId.ProjectionDeleted,
			DataElasticsearchEventId.ProjectionIndexCreationFailed
		];
	}

	#endregion
}
