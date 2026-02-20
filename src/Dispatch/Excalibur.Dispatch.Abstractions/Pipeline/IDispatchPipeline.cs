// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the ordered set of middleware executed when dispatching a message.
/// </summary>
public interface IDispatchPipeline
{
	/// <summary>
	/// Executes the pipeline with the provided message and context.
	/// </summary>
	/// <param name="message"> Message to be dispatched. </param>
	/// <param name="context"> Context associated with the message. </param>
	/// <param name="nextDelegate"> Delegate to invoke when pipeline completes. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The result produced by the pipeline. </returns>
	ValueTask<IMessageResult> ExecuteAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken);
}
