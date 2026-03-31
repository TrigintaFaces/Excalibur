// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.OpenSearch.Diagnostics;

/// <summary>
/// Event IDs for OpenSearch data access (108000-108999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>108000-108099: Client Management</item>
/// <item>108100-108199: Index Operations</item>
/// <item>108200-108299: Document Operations</item>
/// <item>108300-108399: Search/Query</item>
/// <item>108400-108499: Aggregations</item>
/// <item>108500-108599: Bulk Operations</item>
/// <item>108600-108699: Error Handling</item>
/// <item>108700-108799: Security Monitoring</item>
/// <item>108800-108899: Performance/Repository</item>
/// <item>108900-108999: Projection Store</item>
/// </list>
/// </remarks>
internal static class DataOpenSearchEventId
{
	// ========================================
	// 108000-108099: Client Management
	// ========================================

	/// <summary>OpenSearch client created.</summary>
	public const int ClientCreated = 108000;

	/// <summary>OpenSearch client disposed.</summary>
	public const int ClientDisposed = 108001;

	/// <summary>Cluster health checked.</summary>
	public const int ClusterHealthChecked = 108002;

	/// <summary>Node discovered.</summary>
	public const int NodeDiscovered = 108003;

	/// <summary>Connection pool configured.</summary>
	public const int ConnectionPoolConfigured = 108004;

	// ========================================
	// 108100-108149: Index Operations
	// ========================================

	/// <summary>Index created.</summary>
	public const int IndexCreated = 108100;

	/// <summary>Index deleted.</summary>
	public const int IndexDeleted = 108101;

	/// <summary>Index exists checked.</summary>
	public const int IndexExistsChecked = 108102;

	/// <summary>Index mapping updated.</summary>
	public const int IndexMappingUpdated = 108103;

	/// <summary>Index settings updated.</summary>
	public const int IndexSettingsUpdated = 108104;

	/// <summary>Index alias created.</summary>
	public const int IndexAliasCreated = 108105;

	// ========================================
	// 108106-108149: Index State Management (ISM)
	// ========================================

	/// <summary>Creating ISM policy (debug).</summary>
	public const int CreatingIsmPolicy = 108106;

	/// <summary>ISM policy created successfully.</summary>
	public const int IsmPolicyCreated = 108107;

	/// <summary>ISM policy creation failed.</summary>
	public const int IsmPolicyCreationFailed = 108108;

	/// <summary>Exception while creating ISM policy.</summary>
	public const int IsmPolicyCreationException = 108109;

	/// <summary>Deleting ISM policy (debug).</summary>
	public const int DeletingIsmPolicy = 108110;

	/// <summary>ISM policy deleted successfully.</summary>
	public const int IsmPolicyDeleted = 108111;

	/// <summary>ISM policy not found.</summary>
	public const int IsmPolicyNotFound = 108112;

	/// <summary>ISM policy deletion failed.</summary>
	public const int IsmPolicyDeletionFailed = 108113;

	/// <summary>Exception while deleting ISM policy.</summary>
	public const int IsmPolicyDeletionException = 108114;

	/// <summary>Rolling over index (debug).</summary>
	public const int RollingOverIndex = 108115;

	/// <summary>Index rolled over successfully.</summary>
	public const int IndexRolledOver = 108116;

	/// <summary>Index rollover failed.</summary>
	public const int IndexRolloverFailed = 108117;

	/// <summary>Exception while rolling over index.</summary>
	public const int IndexRolloverException = 108118;

	/// <summary>Getting ISM status (debug).</summary>
	public const int GettingIsmStatus = 108119;

	/// <summary>ISM status retrieved successfully.</summary>
	public const int IsmStatusRetrieved = 108120;

	/// <summary>ISM status retrieval failed.</summary>
	public const int IsmStatusFailed = 108121;

	/// <summary>Exception while getting ISM status.</summary>
	public const int IsmStatusException = 108122;

	/// <summary>Moving indices to next phase (debug).</summary>
	public const int MovingToNextPhase = 108123;

	/// <summary>No indices found for phase move.</summary>
	public const int NoIndicesFoundForPhaseMove = 108124;

	/// <summary>Index has no ISM policy assigned.</summary>
	public const int IndexHasNoPolicy = 108125;

	/// <summary>Index is already in final phase.</summary>
	public const int IndexAlreadyInFinalPhase = 108126;

	/// <summary>Index moved to next ISM phase.</summary>
	public const int IndexMovedToNextPhase = 108127;

	/// <summary>Index move to next phase failed.</summary>
	public const int IndexMoveToNextPhaseFailed = 108128;

	/// <summary>Exception while moving to next phase.</summary>
	public const int MoveToNextPhaseException = 108129;

	// ========================================
	// 108200-108299: Document Operations
	// ========================================

	/// <summary>Document indexed.</summary>
	public const int DocumentIndexed = 108200;

	/// <summary>Document retrieved.</summary>
	public const int DocumentRetrieved = 108201;

	/// <summary>Document updated.</summary>
	public const int DocumentUpdated = 108202;

	/// <summary>Document deleted.</summary>
	public const int DocumentDeleted = 108203;

	/// <summary>Document source retrieved.</summary>
	public const int DocumentSourceRetrieved = 108204;

	/// <summary>Document exists checked.</summary>
	public const int DocumentExistsChecked = 108205;

	// ========================================
	// 108300-108399: Search/Query
	// ========================================

	/// <summary>Search executing.</summary>
	public const int SearchExecuting = 108300;

	/// <summary>Search executed.</summary>
	public const int SearchExecuted = 108301;

	/// <summary>Multi-search executed.</summary>
	public const int MultiSearchExecuted = 108302;

	/// <summary>Scroll search started.</summary>
	public const int ScrollSearchStarted = 108303;

	/// <summary>Scroll search continued.</summary>
	public const int ScrollSearchContinued = 108304;

	/// <summary>Scroll search cleared.</summary>
	public const int ScrollSearchCleared = 108305;

	// ========================================
	// 108400-108499: Aggregations
	// ========================================

	/// <summary>Aggregation executed.</summary>
	public const int AggregationExecuted = 108400;

	/// <summary>Terms aggregation executed.</summary>
	public const int TermsAggregationExecuted = 108401;

	/// <summary>Date histogram executed.</summary>
	public const int DateHistogramExecuted = 108402;

	/// <summary>Nested aggregation executed.</summary>
	public const int NestedAggregationExecuted = 108403;

	// ========================================
	// 108500-108599: Bulk Operations
	// ========================================

	/// <summary>Bulk operation started.</summary>
	public const int BulkOperationStarted = 108500;

	/// <summary>Bulk operation completed.</summary>
	public const int BulkOperationCompleted = 108501;

	/// <summary>Bulk item succeeded.</summary>
	public const int BulkItemSucceeded = 108502;

	/// <summary>Bulk item failed.</summary>
	public const int BulkItemFailed = 108503;

	/// <summary>Bulk all helper executing.</summary>
	public const int BulkAllHelperExecuting = 108504;

	// ========================================
	// 108600-108699: Error Handling
	// ========================================

	/// <summary>Index not found.</summary>
	public const int IndexNotFound = 108600;

	/// <summary>Document not found.</summary>
	public const int DocumentNotFound = 108601;

	/// <summary>Version conflict.</summary>
	public const int VersionConflict = 108602;

	/// <summary>OpenSearch exception occurred.</summary>
	public const int OpenSearchException = 108603;

	/// <summary>Request timeout.</summary>
	public const int RequestTimeout = 108604;

	/// <summary>Circuit breaker triggered.</summary>
	public const int CircuitBreakerTriggered = 108605;

	// ========================================
	// 108700-108799: Security Monitoring
	// ========================================

	/// <summary>Security monitoring service started.</summary>
	public const int SecurityMonitoringStarted = 108700;

	/// <summary>Security monitoring service stopped.</summary>
	public const int SecurityMonitoringStopped = 108701;

	// ========================================
	// 108800-108899: Performance/Repository
	// ========================================

	/// <summary>Slow query detected in repository.</summary>
	public const int RepositorySlowQuery = 108800;

	/// <summary>Cache warming error occurred.</summary>
	public const int CacheWarmingError = 108801;

	// ========================================
	// 108900-108999: Query Optimization
	// ========================================

	/// <summary>Search request optimization failed.</summary>
	public const int SearchRequestOptimizationFailed = 108900;

	/// <summary>Query execution analysis failed.</summary>
	public const int QueryExecutionAnalysisFailed = 108901;

	/// <summary>Slow query detected during analysis.</summary>
	public const int SlowQueryDetected = 108902;

	// ========================================
	// 108910-108919: Projection Store
	// ========================================

	/// <summary>Projection store initialized.</summary>
	public const int ProjectionStoreInitialized = 108910;

	/// <summary>Projection upserted.</summary>
	public const int ProjectionUpserted = 108911;

	/// <summary>Projection deleted.</summary>
	public const int ProjectionDeleted = 108912;

	/// <summary>Failed to create projection index.</summary>
	public const int ProjectionIndexCreationFailed = 108913;
}
