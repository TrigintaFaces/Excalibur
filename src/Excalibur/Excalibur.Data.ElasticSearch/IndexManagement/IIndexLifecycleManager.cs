// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for managing the lifecycle of Elasticsearch indices including rollover, aging, and deletion.
/// </summary>
public interface IIndexLifecycleManager
{
	/// <summary>
	/// Creates an index lifecycle policy in Elasticsearch.
	/// </summary>
	/// <param name="policyName"> The name of the lifecycle policy. </param>
	/// <param name="policy"> The lifecycle policy configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateLifecyclePolicyAsync(string policyName, IndexLifecyclePolicy policy, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an index lifecycle policy from Elasticsearch.
	/// </summary>
	/// <param name="policyName"> The name of the lifecycle policy to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteLifecyclePolicyAsync(string policyName, CancellationToken cancellationToken);

	/// <summary>
	/// Performs index rollover based on the specified conditions.
	/// </summary>
	/// <param name="aliasName"> The alias name to rollover. </param>
	/// <param name="rolloverConditions"> The conditions for rollover. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IndexRolloverResult}" /> containing rollover results. </returns>
	Task<IndexRolloverResult> RolloverIndexAsync(string aliasName, RolloverConditions rolloverConditions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of indices managed by lifecycle policies.
	/// </summary>
	/// <param name="indexPattern"> The index pattern to check. If null, checks all indices. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IEnumerable}" /> containing index lifecycle status information. </returns>
	Task<IEnumerable<IndexLifecycleStatus>> GetIndexLifecycleStatusAsync(
		string? indexPattern,
		CancellationToken cancellationToken);

	/// <summary>
	/// Forces a lifecycle policy to move to the next phase for specified indices.
	/// </summary>
	/// <param name="indexPattern"> The pattern of indices to move to next phase. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> MoveToNextPhaseAsync(string indexPattern, CancellationToken cancellationToken);
}
