// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.OpenSearch.IndexManagement;

internal partial class IndexLifecycleManager
{
	// ========================================
	// ISM Policy Creation
	// ========================================

	[LoggerMessage(DataOpenSearchEventId.CreatingIsmPolicy, LogLevel.Debug,
		"Creating ISM policy: {PolicyName}")]
	private partial void LogCreatingIsmPolicy(string policyName);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyCreated, LogLevel.Information,
		"ISM policy created successfully: {PolicyName}")]
	private partial void LogIsmPolicyCreated(string policyName);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyCreationFailed, LogLevel.Error,
		"Failed to create ISM policy {PolicyName}: {Reason}")]
	private partial void LogIsmPolicyCreationFailed(string policyName, string reason);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyCreationException, LogLevel.Error,
		"Exception while creating ISM policy {PolicyName}")]
	private partial void LogIsmPolicyCreationException(string policyName, Exception exception);

	// ========================================
	// ISM Policy Deletion
	// ========================================

	[LoggerMessage(DataOpenSearchEventId.DeletingIsmPolicy, LogLevel.Debug,
		"Deleting ISM policy: {PolicyName}")]
	private partial void LogDeletingIsmPolicy(string policyName);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyDeleted, LogLevel.Information,
		"ISM policy deleted successfully: {PolicyName}")]
	private partial void LogIsmPolicyDeleted(string policyName);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyNotFound, LogLevel.Warning,
		"ISM policy not found: {PolicyName}")]
	private partial void LogIsmPolicyNotFound(string policyName);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyDeletionFailed, LogLevel.Error,
		"Failed to delete ISM policy {PolicyName}: {Reason}")]
	private partial void LogIsmPolicyDeletionFailed(string policyName, string reason);

	[LoggerMessage(DataOpenSearchEventId.IsmPolicyDeletionException, LogLevel.Error,
		"Exception while deleting ISM policy {PolicyName}")]
	private partial void LogIsmPolicyDeletionException(string policyName, Exception exception);

	// ========================================
	// Index Rollover
	// ========================================

	[LoggerMessage(DataOpenSearchEventId.RollingOverIndex, LogLevel.Debug,
		"Rolling over index for alias: {AliasName}")]
	private partial void LogRollingOverIndex(string aliasName);

	[LoggerMessage(DataOpenSearchEventId.IndexRolledOver, LogLevel.Information,
		"Index rollover completed for alias {AliasName}. RolledOver: {RolledOver}, NewIndex: {NewIndex}")]
	private partial void LogIndexRolledOver(string aliasName, bool rolledOver, string newIndex);

	[LoggerMessage(DataOpenSearchEventId.IndexRolloverFailed, LogLevel.Error,
		"Failed to rollover index for alias {AliasName}: {Reason}")]
	private partial void LogIndexRolloverFailed(string aliasName, string reason);

	[LoggerMessage(DataOpenSearchEventId.IndexRolloverException, LogLevel.Error,
		"Exception while rolling over index for alias {AliasName}")]
	private partial void LogIndexRolloverException(string aliasName, Exception exception);

	// ========================================
	// ISM Status
	// ========================================

	[LoggerMessage(DataOpenSearchEventId.GettingIsmStatus, LogLevel.Debug,
		"Getting ISM status for pattern: {Pattern}")]
	private partial void LogGettingIsmStatus(string pattern);

	[LoggerMessage(DataOpenSearchEventId.IsmStatusRetrieved, LogLevel.Information,
		"ISM status retrieved for pattern {Pattern}. Found {Count} indices")]
	private partial void LogIsmStatusRetrieved(string pattern, int count);

	[LoggerMessage(DataOpenSearchEventId.IsmStatusFailed, LogLevel.Error,
		"Failed to get ISM status for pattern {Pattern}: {Reason}")]
	private partial void LogIsmStatusFailed(string pattern, string reason);

	[LoggerMessage(DataOpenSearchEventId.IsmStatusException, LogLevel.Error,
		"Exception while getting ISM status for pattern {Pattern}")]
	private partial void LogIsmStatusException(string pattern, Exception exception);

	// ========================================
	// Move to Next Phase
	// ========================================

	[LoggerMessage(DataOpenSearchEventId.MovingToNextPhase, LogLevel.Debug,
		"Moving indices to next phase for pattern: {Pattern}")]
	private partial void LogMovingToNextPhase(string pattern);

	[LoggerMessage(DataOpenSearchEventId.NoIndicesFoundForPhaseMove, LogLevel.Warning,
		"No indices found for phase move with pattern: {Pattern}")]
	private partial void LogNoIndicesFoundForPhaseMove(string pattern);

	[LoggerMessage(DataOpenSearchEventId.IndexHasNoPolicy, LogLevel.Warning,
		"Index {IndexName} has no ISM policy assigned, skipping phase move")]
	private partial void LogIndexHasNoPolicy(string indexName);

	[LoggerMessage(DataOpenSearchEventId.IndexAlreadyInFinalPhase, LogLevel.Information,
		"Index {IndexName} is already in final phase ({Phase}), skipping")]
	private partial void LogIndexAlreadyInFinalPhase(string indexName, string phase);

	[LoggerMessage(DataOpenSearchEventId.IndexMovedToNextPhase, LogLevel.Information,
		"Index {IndexName} moved from {FromPhase} to {ToPhase}")]
	private partial void LogIndexMovedToNextPhase(string indexName, string fromPhase, string toPhase);

	[LoggerMessage(DataOpenSearchEventId.IndexMoveToNextPhaseFailed, LogLevel.Error,
		"Failed to move index {IndexName} from phase {Phase}: {Reason}")]
	private partial void LogIndexMoveToNextPhaseFailed(string indexName, string phase, string reason);

	[LoggerMessage(DataOpenSearchEventId.MoveToNextPhaseException, LogLevel.Error,
		"Exception while moving indices to next phase for pattern {Pattern}")]
	private partial void LogMoveToNextPhaseException(string pattern, Exception exception);
}
