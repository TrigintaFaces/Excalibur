// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Executes scheduled erasure requests by deleting encryption keys.
/// </summary>
/// <remarks>
/// This interface is internal — it is used only by the erasure scheduler background service
/// to execute erasure requests after their grace period has expired. Consumer code should use
/// <see cref="IErasureService"/> for submitting, querying, and cancelling erasure requests.
/// </remarks>
internal interface IErasureExecutor
{
	/// <summary>
	/// Executes a scheduled erasure request by deleting encryption keys.
	/// </summary>
	/// <param name="requestId">The erasure request tracking ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Execution result with details of keys deleted.</returns>
	Task<ErasureExecutionResult> ExecuteAsync(
		Guid requestId,
		CancellationToken cancellationToken);
}
