// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Middleware interface for processing contexts in a pipeline.
/// </summary>
/// <typeparam name="TContext"> The type of context that flows through the middleware pipeline. </typeparam>
public interface IMiddleware<TContext>
{
	/// <summary>
	/// Invokes the middleware with the specified context and next delegate.
	/// </summary>
	/// <param name="context"> The context being processed through the middleware pipeline. </param>
	/// <param name="nextDelegate"> The function to call the next middleware in the pipeline. </param>
	/// <returns> A task representing the asynchronous middleware operation. </returns>
	Task InvokeAsync(TContext context, Func<TContext, Task> nextDelegate);
}
