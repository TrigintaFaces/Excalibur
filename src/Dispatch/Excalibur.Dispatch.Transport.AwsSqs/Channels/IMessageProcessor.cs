// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Message processor interface for SQS messages.
/// </summary>
public interface IMessageProcessor<T>
{
	/// <summary>
	/// </summary>
	/// <param name="message"> </param>
	/// <param name="cancellationToken"> </param>
	/// <returns> A <see cref="Task{TResult}" /> representing the result of the asynchronous operation. </returns>
	Task<ProcessingResult> ProcessAsync(T message, CancellationToken cancellationToken);
}
