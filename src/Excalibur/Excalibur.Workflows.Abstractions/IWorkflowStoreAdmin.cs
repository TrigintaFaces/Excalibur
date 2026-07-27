// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// An optional extension point for administrative querying of durable workflow instances — listing and
/// summarizing them (for example from an operational dashboard).
/// </summary>
/// <remarks>
/// This is a consumer/provider extension point, not a built-in feature: the framework ships no
/// implementation. A workflow store that can enumerate instances implements this interface and registers it
/// in dependency injection to enable workflow listing; callers resolve it optionally with <c>GetService</c>
/// (nullable) and fail open when no implementation is registered rather than requiring one. Because it is a
/// read-only query surface it makes no safety guarantee — its absence is a visible no-op (no listing), never
/// a silently weakened control. In a multi-tenant host, an implementation scopes results to the ambient
/// tenant so a query never crosses tenant boundaries. Querying never mutates an instance.
/// </remarks>
public interface IWorkflowStoreAdmin
{
	/// <summary>
	/// Queries a paged list of workflow-instance summaries matching the given filter.
	/// </summary>
	/// <param name="filter">The filter and paging window to apply.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The matching page of workflow-instance summaries.</returns>
	ValueTask<IReadOnlyList<WorkflowInstanceSummary>> QueryWorkflowsAsync(
		WorkflowQueryFilter filter,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the summary for a single workflow instance, or <see langword="null"/> when no instance with the
	/// given identifier exists.
	/// </summary>
	/// <param name="instanceId">The target workflow instance identifier.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The instance summary, or <see langword="null"/> when the instance does not exist.</returns>
	ValueTask<WorkflowInstanceSummary?> GetSummaryAsync(string instanceId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets aggregate counts of workflow instances by lifecycle status.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The aggregate workflow-instance statistics.</returns>
	ValueTask<WorkflowStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
}
