// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Storage abstraction for erasure requests and certificates.
/// </summary>
/// <remarks>
/// Implementations should use Dapper (NOT EntityFramework Core) per project constraints.
/// </remarks>
public interface IErasureStore
{
	/// <summary>
	/// Saves a new erasure request.
	/// </summary>
	/// <param name="request">The erasure request to save.</param>
	/// <param name="scheduledExecutionTime">When the erasure is scheduled to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The erasure status, or null if not found.</returns>
	Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="status">The new status.</param>
	/// <param name="errorMessage">Optional error message if failed.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if updated, false if not found.</returns>
	Task<bool> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		string? errorMessage,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure completion.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="keysDeleted">Number of keys deleted.</param>
	/// <param name="recordsAffected">Number of records affected.</param>
	/// <param name="certificateId">The generated certificate ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure cancellation.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="reason">Cancellation reason.</param>
	/// <param name="cancelledBy">Who cancelled.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if cancelled, false if not found or already executed.</returns>
	Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="IErasureCertificateStore"/>, <see cref="IErasureQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
