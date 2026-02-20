// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Diagnostics;

/// <summary>
/// Event IDs for Elasticsearch data access (106000-106999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>106000-106099: Client Management</item>
/// <item>106100-106199: Index Operations</item>
/// <item>106200-106299: Document Operations</item>
/// <item>106300-106399: Search/Query</item>
/// <item>106400-106499: Aggregations</item>
/// <item>106500-106599: Bulk Operations</item>
/// <item>106600-106699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataElasticsearchEventId
{
	// ========================================
	// 106000-106099: Client Management
	// ========================================

	/// <summary>Elasticsearch client created.</summary>
	public const int ClientCreated = 106000;

	/// <summary>Elasticsearch client disposed.</summary>
	public const int ClientDisposed = 106001;

	/// <summary>Cluster health checked.</summary>
	public const int ClusterHealthChecked = 106002;

	/// <summary>Node discovered.</summary>
	public const int NodeDiscovered = 106003;

	/// <summary>Connection pool configured.</summary>
	public const int ConnectionPoolConfigured = 106004;

	// ========================================
	// 106100-106199: Index Operations
	// ========================================

	/// <summary>Index created.</summary>
	public const int IndexCreated = 106100;

	/// <summary>Index deleted.</summary>
	public const int IndexDeleted = 106101;

	/// <summary>Index exists checked.</summary>
	public const int IndexExistsChecked = 106102;

	/// <summary>Index mapping updated.</summary>
	public const int IndexMappingUpdated = 106103;

	/// <summary>Index settings updated.</summary>
	public const int IndexSettingsUpdated = 106104;

	/// <summary>Index alias created.</summary>
	public const int IndexAliasCreated = 106105;

	// ========================================
	// 106106-106149: Index Lifecycle Management (ILM)
	// ========================================

	/// <summary>Creating lifecycle policy (debug).</summary>
	public const int CreatingLifecyclePolicy = 106106;

	/// <summary>Index lifecycle policy created successfully.</summary>
	public const int LifecyclePolicyCreated = 106107;

	/// <summary>Index lifecycle policy creation failed.</summary>
	public const int LifecyclePolicyCreationFailed = 106108;

	/// <summary>Exception while creating lifecycle policy.</summary>
	public const int LifecyclePolicyCreationException = 106109;

	/// <summary>Deleting lifecycle policy (debug).</summary>
	public const int DeletingLifecyclePolicy = 106110;

	/// <summary>Index lifecycle policy deleted successfully.</summary>
	public const int LifecyclePolicyDeleted = 106111;

	/// <summary>Index lifecycle policy not found.</summary>
	public const int LifecyclePolicyNotFound = 106112;

	/// <summary>Index lifecycle policy deletion failed.</summary>
	public const int LifecyclePolicyDeletionFailed = 106113;

	/// <summary>Exception while deleting lifecycle policy.</summary>
	public const int LifecyclePolicyDeletionException = 106114;

	/// <summary>Rolling over index (debug).</summary>
	public const int RollingOverIndex = 106115;

	/// <summary>Index rolled over successfully.</summary>
	public const int IndexRolledOver = 106116;

	/// <summary>Index rollover failed.</summary>
	public const int IndexRolloverFailed = 106117;

	/// <summary>Exception while rolling over index.</summary>
	public const int IndexRolloverException = 106118;

	/// <summary>Getting lifecycle status (debug).</summary>
	public const int GettingLifecycleStatus = 106119;

	/// <summary>Index lifecycle status retrieved successfully.</summary>
	public const int LifecycleStatusRetrieved = 106120;

	/// <summary>Index lifecycle status retrieval failed.</summary>
	public const int LifecycleStatusFailed = 106121;

	/// <summary>Exception while getting lifecycle status.</summary>
	public const int LifecycleStatusException = 106122;

	/// <summary>Moving indices to next phase (debug).</summary>
	public const int MovingToNextPhase = 106123;

	/// <summary>No indices found for phase move.</summary>
	public const int NoIndicesFoundForPhaseMove = 106124;

	/// <summary>Index has no ILM policy assigned.</summary>
	public const int IndexHasNoPolicy = 106125;

	/// <summary>Index is already in final phase.</summary>
	public const int IndexAlreadyInFinalPhase = 106126;

	/// <summary>Index moved to next lifecycle phase.</summary>
	public const int IndexMovedToNextPhase = 106127;

	/// <summary>Index move to next phase failed.</summary>
	public const int IndexMoveToNextPhaseFailed = 106128;

	/// <summary>Exception while moving to next phase.</summary>
	public const int MoveToNextPhaseException = 106129;

	// ========================================
	// 106200-106299: Document Operations
	// ========================================

	/// <summary>Document indexed.</summary>
	public const int DocumentIndexed = 106200;

	/// <summary>Document retrieved.</summary>
	public const int DocumentRetrieved = 106201;

	/// <summary>Document updated.</summary>
	public const int DocumentUpdated = 106202;

	/// <summary>Document deleted.</summary>
	public const int DocumentDeleted = 106203;

	/// <summary>Document source retrieved.</summary>
	public const int DocumentSourceRetrieved = 106204;

	/// <summary>Document exists checked.</summary>
	public const int DocumentExistsChecked = 106205;

	// ========================================
	// 106300-106399: Search/Query
	// ========================================

	/// <summary>Search executing.</summary>
	public const int SearchExecuting = 106300;

	/// <summary>Search executed.</summary>
	public const int SearchExecuted = 106301;

	/// <summary>Multi-search executed.</summary>
	public const int MultiSearchExecuted = 106302;

	/// <summary>Scroll search started.</summary>
	public const int ScrollSearchStarted = 106303;

	/// <summary>Scroll search continued.</summary>
	public const int ScrollSearchContinued = 106304;

	/// <summary>Scroll search cleared.</summary>
	public const int ScrollSearchCleared = 106305;

	// ========================================
	// 106400-106499: Aggregations
	// ========================================

	/// <summary>Aggregation executed.</summary>
	public const int AggregationExecuted = 106400;

	/// <summary>Terms aggregation executed.</summary>
	public const int TermsAggregationExecuted = 106401;

	/// <summary>Date histogram executed.</summary>
	public const int DateHistogramExecuted = 106402;

	/// <summary>Nested aggregation executed.</summary>
	public const int NestedAggregationExecuted = 106403;

	// ========================================
	// 106500-106599: Bulk Operations
	// ========================================

	/// <summary>Bulk operation started.</summary>
	public const int BulkOperationStarted = 106500;

	/// <summary>Bulk operation completed.</summary>
	public const int BulkOperationCompleted = 106501;

	/// <summary>Bulk item succeeded.</summary>
	public const int BulkItemSucceeded = 106502;

	/// <summary>Bulk item failed.</summary>
	public const int BulkItemFailed = 106503;

	/// <summary>Bulk all helper executing.</summary>
	public const int BulkAllHelperExecuting = 106504;

	// ========================================
	// 106600-106699: Error Handling
	// ========================================

	/// <summary>Index not found.</summary>
	public const int IndexNotFound = 106600;

	/// <summary>Document not found.</summary>
	public const int DocumentNotFound = 106601;

	/// <summary>Version conflict.</summary>
	public const int VersionConflict = 106602;

	/// <summary>Elasticsearch exception occurred.</summary>
	public const int ElasticsearchException = 106603;

	/// <summary>Request timeout.</summary>
	public const int RequestTimeout = 106604;

	/// <summary>Circuit breaker triggered.</summary>
	public const int CircuitBreakerTriggered = 106605;

	// ========================================
	// 106700-106799: Security Monitoring
	// ========================================

	/// <summary>Security monitoring service started.</summary>
	public const int SecurityMonitoringStarted = 106700;

	/// <summary>Security monitoring service stopped.</summary>
	public const int SecurityMonitoringStopped = 106701;

	// ========================================
	// 106800-106899: Performance/Repository
	// ========================================

	/// <summary>Slow query detected in repository.</summary>
	public const int RepositorySlowQuery = 106800;

	/// <summary>Cache warming error occurred.</summary>
	public const int CacheWarmingError = 106801;

	// ========================================
	// 106900-106999: Query Optimization
	// ========================================

	/// <summary>Search request optimization failed.</summary>
	public const int SearchRequestOptimizationFailed = 106900;

	/// <summary>Query execution analysis failed.</summary>
	public const int QueryExecutionAnalysisFailed = 106901;

	/// <summary>Slow query detected during analysis.</summary>
	public const int SlowQueryDetected = 106902;

	// ========================================
	// 106910-106919: Projection Store
	// ========================================

	/// <summary>Projection store initialized.</summary>
	public const int ProjectionStoreInitialized = 106910;

	/// <summary>Projection upserted.</summary>
	public const int ProjectionUpserted = 106911;

	/// <summary>Projection deleted.</summary>
	public const int ProjectionDeleted = 106912;

	/// <summary>Failed to create projection index.</summary>
	public const int ProjectionIndexCreationFailed = 106913;
}
