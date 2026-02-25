// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for building middleware pipelines.
/// </summary>
/// <typeparam name="TContext"> The type of context that flows through the middleware pipeline. </typeparam>
public interface IMiddlewarePipelineBuilder<TContext>
{
	/// <summary>
	/// Adds a middleware delegate to the pipeline that accepts a context and next delegate.
	/// </summary>
	/// <param name="middleware"> The middleware delegate that processes context and calls the next middleware. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IMiddlewarePipelineBuilder<TContext> Use(Func<TContext, Func<TContext, Task>, Task> middleware);

	/// <summary>
	/// Adds a typed middleware component to the pipeline that implements the IMiddleware interface.
	/// </summary>
	/// <typeparam name="TMiddleware"> The type of middleware to add, must implement IMiddleware. </typeparam>
	/// <returns> The pipeline builder for method chaining. </returns>
	IMiddlewarePipelineBuilder<TContext> Use<TMiddleware>()
		where TMiddleware : IMiddleware<TContext>;

	/// <summary>
	/// Builds the final middleware pipeline as a single delegate that processes the context.
	/// </summary>
	/// <returns> A delegate that represents the complete middleware pipeline. </returns>
	Func<TContext, Task> Build();
}
