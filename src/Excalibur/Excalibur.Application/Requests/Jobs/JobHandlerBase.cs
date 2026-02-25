// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Application.Requests.Jobs;

/// <summary>
/// Represents a base class for handling jobs of type <typeparamref name="TRequest" />.
/// </summary>
/// <typeparam name="TRequest"> The type of the job to handle, which must implement <see cref="IJob" />. </typeparam>
public abstract class JobHandlerBase<TRequest> : IActionHandler<TRequest, JobResult>
	where TRequest : IJob
{
	/// <summary>
	/// Handles the specified job action.
	/// </summary>
	/// <param name="action"> The job action to handle. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the <see cref="JobResult" />. </returns>
	public abstract Task<JobResult> HandleAsync(TRequest action, CancellationToken cancellationToken);
}
