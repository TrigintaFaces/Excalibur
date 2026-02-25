// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Manages the rebuilding of projections from event stores or source data.
/// </summary>
public interface IProjectionRebuildManager
{
	/// <summary>
	/// Initiates a full rebuild of a projection from the event store.
	/// </summary>
	/// <param name="request"> The rebuild request containing projection details and options. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A rebuild operation result containing the operation identifier and status. </returns>
	Task<ProjectionRebuildResult> StartRebuildAsync(
		ProjectionRebuildRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current status of a rebuild operation.
	/// </summary>
	/// <param name="operationId"> The identifier of the rebuild operation. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The current status of the rebuild operation. </returns>
	Task<ProjectionRebuildStatus> GetRebuildStatusAsync(
		string operationId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Cancels an ongoing rebuild operation.
	/// </summary>
	/// <param name="operationId"> The identifier of the rebuild operation to cancel. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the operation was successfully cancelled; otherwise, false. </returns>
	Task<bool> CancelRebuildAsync(
		string operationId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all rebuild operations within a specified time range.
	/// </summary>
	/// <param name="fromDate"> The start date for the query. </param>
	/// <param name="toDate"> The end date for the query. </param>
	/// <param name="projectionType"> Optional filter by projection type. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of rebuild operation summaries. </returns>
	Task<IEnumerable<ProjectionRebuildSummary>> ListRebuildOperationsAsync(
		DateTime fromDate,
		DateTime toDate,
		string? projectionType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that a projection can be rebuilt without Tests.Shared.Handlers.TestInfrastructure.
	/// </summary>
	/// <param name="projectionType"> The type of projection to validate. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A validation result indicating whether the rebuild is possible. </returns>
	Task<ProjectionRebuildValidation> ValidateRebuildAsync(
		string projectionType,
		CancellationToken cancellationToken);
}
