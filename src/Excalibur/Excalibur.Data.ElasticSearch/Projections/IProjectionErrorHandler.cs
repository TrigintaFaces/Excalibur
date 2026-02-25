// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines the contract for handling projection-related errors in ElasticSearch operations.
/// </summary>
public interface IProjectionErrorHandler
{
	/// <summary>
	/// Handles an error that occurred during a projection operation.
	/// </summary>
	/// <param name="context"> The context of the failed projection operation. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous error handling operation. </returns>
	Task HandleProjectionErrorAsync(ProjectionErrorContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Handles errors from bulk operations where some documents may have succeeded.
	/// </summary>
	/// <param name="context"> The bulk operation error context containing both successful and failed items. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous bulk error handling operation. </returns>
	Task HandleBulkOperationErrorsAsync(BulkOperationErrorContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves projection errors for analysis and potential reprocessing.
	/// </summary>
	/// <param name="fromDate"> The start date for retrieving error records. </param>
	/// <param name="toDate"> The end date for retrieving error records. </param>
	/// <param name="projectionType"> Optional filter by projection type. </param>
	/// <param name="maxResults"> Maximum number of results to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of projection error records matching the criteria. </returns>
	Task<IEnumerable<ProjectionErrorRecord>> GetProjectionErrorsAsync(
		DateTime fromDate,
		DateTime toDate,
		string? projectionType,
		int maxResults,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks projection errors as resolved after successful reprocessing.
	/// </summary>
	/// <param name="errorIds"> The identifiers of the resolved errors. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The number of errors marked as resolved. </returns>
	Task<int> MarkErrorsAsResolvedAsync(IEnumerable<string> errorIds, CancellationToken cancellationToken);
}
