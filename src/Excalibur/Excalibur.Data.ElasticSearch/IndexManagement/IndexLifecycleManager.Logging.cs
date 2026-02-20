// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

public partial class IndexLifecycleManager
{
	// ========================================
	// Lifecycle Policy Creation
	// ========================================

	[LoggerMessage(DataElasticsearchEventId.CreatingLifecyclePolicy, LogLevel.Debug,
		"Creating lifecycle policy: {PolicyName}")]
	private partial void LogCreatingLifecyclePolicy(string policyName);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyCreated, LogLevel.Information,
		"Lifecycle policy created successfully: {PolicyName}")]
	private partial void LogLifecyclePolicyCreated(string policyName);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyCreationFailed, LogLevel.Error,
		"Failed to create lifecycle policy {PolicyName}: {Reason}")]
	private partial void LogLifecyclePolicyCreationFailed(string policyName, string reason);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyCreationException, LogLevel.Error,
		"Exception while creating lifecycle policy {PolicyName}")]
	private partial void LogLifecyclePolicyCreationException(string policyName, Exception exception);

	// ========================================
	// Lifecycle Policy Deletion
	// ========================================

	[LoggerMessage(DataElasticsearchEventId.DeletingLifecyclePolicy, LogLevel.Debug,
		"Deleting lifecycle policy: {PolicyName}")]
	private partial void LogDeletingLifecyclePolicy(string policyName);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyDeleted, LogLevel.Information,
		"Lifecycle policy deleted successfully: {PolicyName}")]
	private partial void LogLifecyclePolicyDeleted(string policyName);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyNotFound, LogLevel.Warning,
		"Lifecycle policy not found: {PolicyName}")]
	private partial void LogLifecyclePolicyNotFound(string policyName);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyDeletionFailed, LogLevel.Error,
		"Failed to delete lifecycle policy {PolicyName}: {Reason}")]
	private partial void LogLifecyclePolicyDeletionFailed(string policyName, string reason);

	[LoggerMessage(DataElasticsearchEventId.LifecyclePolicyDeletionException, LogLevel.Error,
		"Exception while deleting lifecycle policy {PolicyName}")]
	private partial void LogLifecyclePolicyDeletionException(string policyName, Exception exception);

	// ========================================
	// Index Rollover
	// ========================================

	[LoggerMessage(DataElasticsearchEventId.RollingOverIndex, LogLevel.Debug,
		"Rolling over index for alias: {AliasName}")]
	private partial void LogRollingOverIndex(string aliasName);

	[LoggerMessage(DataElasticsearchEventId.IndexRolledOver, LogLevel.Information,
		"Index rollover completed for alias {AliasName}. RolledOver: {RolledOver}, NewIndex: {NewIndex}")]
	private partial void LogIndexRolledOver(string aliasName, bool rolledOver, string newIndex);

	[LoggerMessage(DataElasticsearchEventId.IndexRolloverFailed, LogLevel.Error,
		"Failed to rollover index for alias {AliasName}: {Reason}")]
	private partial void LogIndexRolloverFailed(string aliasName, string reason);

	[LoggerMessage(DataElasticsearchEventId.IndexRolloverException, LogLevel.Error,
		"Exception while rolling over index for alias {AliasName}")]
	private partial void LogIndexRolloverException(string aliasName, Exception exception);

	// ========================================
	// Lifecycle Status
	// ========================================

	[LoggerMessage(DataElasticsearchEventId.GettingLifecycleStatus, LogLevel.Debug,
		"Getting lifecycle status for pattern: {Pattern}")]
	private partial void LogGettingLifecycleStatus(string pattern);

	[LoggerMessage(DataElasticsearchEventId.LifecycleStatusRetrieved, LogLevel.Information,
		"Lifecycle status retrieved for pattern {Pattern}. Found {Count} indices")]
	private partial void LogLifecycleStatusRetrieved(string pattern, int count);

	[LoggerMessage(DataElasticsearchEventId.LifecycleStatusFailed, LogLevel.Error,
		"Failed to get lifecycle status for pattern {Pattern}: {Reason}")]
	private partial void LogLifecycleStatusFailed(string pattern, string reason);

	[LoggerMessage(DataElasticsearchEventId.LifecycleStatusException, LogLevel.Error,
		"Exception while getting lifecycle status for pattern {Pattern}")]
	private partial void LogLifecycleStatusException(string pattern, Exception exception);

	// ========================================
	// Move to Next Phase
	// ========================================

	[LoggerMessage(DataElasticsearchEventId.MovingToNextPhase, LogLevel.Debug,
		"Moving indices to next phase for pattern: {Pattern}")]
	private partial void LogMovingToNextPhase(string pattern);

	[LoggerMessage(DataElasticsearchEventId.NoIndicesFoundForPhaseMove, LogLevel.Warning,
		"No indices found for phase move with pattern: {Pattern}")]
	private partial void LogNoIndicesFoundForPhaseMove(string pattern);

	[LoggerMessage(DataElasticsearchEventId.IndexHasNoPolicy, LogLevel.Warning,
		"Index {IndexName} has no ILM policy assigned, skipping phase move")]
	private partial void LogIndexHasNoPolicy(string indexName);

	[LoggerMessage(DataElasticsearchEventId.IndexAlreadyInFinalPhase, LogLevel.Information,
		"Index {IndexName} is already in final phase ({Phase}), skipping")]
	private partial void LogIndexAlreadyInFinalPhase(string indexName, string phase);

	[LoggerMessage(DataElasticsearchEventId.IndexMovedToNextPhase, LogLevel.Information,
		"Index {IndexName} moved from {FromPhase} to {ToPhase}")]
	private partial void LogIndexMovedToNextPhase(string indexName, string fromPhase, string toPhase);

	[LoggerMessage(DataElasticsearchEventId.IndexMoveToNextPhaseFailed, LogLevel.Error,
		"Failed to move index {IndexName} from phase {Phase}: {Reason}")]
	private partial void LogIndexMoveToNextPhaseFailed(string indexName, string phase, string reason);

	[LoggerMessage(DataElasticsearchEventId.MoveToNextPhaseException, LogLevel.Error,
		"Exception while moving indices to next phase for pattern {Pattern}")]
	private partial void LogMoveToNextPhaseException(string pattern, Exception exception);
}
