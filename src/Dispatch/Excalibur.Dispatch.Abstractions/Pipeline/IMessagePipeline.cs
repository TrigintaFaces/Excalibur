// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a pipeline of middleware components that process messages.
/// </summary>
public interface IMessagePipeline
{
	/// <summary>
	/// Gets the middleware components in this pipeline.
	/// </summary>
	/// <value> The ordered list of middleware types. </value>
	IReadOnlyList<Type> MiddlewareTypes { get; }

	/// <summary>
	/// Executes the message pipeline with the given context.
	/// </summary>
	/// <param name="context"> The message context to process. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ExecuteAsync(IMessageContext context, CancellationToken cancellationToken);
}
