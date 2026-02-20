// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Abstractions;

/// <summary>
/// Represents a background job that can be executed by the job scheduler.
/// </summary>
public interface IBackgroundJob
{
	/// <summary>
	/// Executes the background job.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Represents a background job with context data.
/// </summary>
/// <typeparam name="TContext"> The type of context data. </typeparam>
public interface IBackgroundJob<TContext>
	where TContext : class
{
	/// <summary>
	/// Executes the background job with the provided context.
	/// </summary>
	/// <param name="context"> The job execution context. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ExecuteAsync(TContext context, CancellationToken cancellationToken);
}
