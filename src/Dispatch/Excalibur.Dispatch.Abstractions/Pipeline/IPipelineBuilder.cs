// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Fluent builder for configuring message processing pipelines.
/// </summary>
public interface IPipelineBuilder
{
	/// <summary>
	/// Gets the name of this pipeline builder.
	/// </summary>
	/// <value> The name assigned to the pipeline builder. </value>
	string Name { get; }

	/// <summary>
	/// Adds middleware of the specified type to the pipeline.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder Use<TMiddleware>()
		where TMiddleware : IDispatchMiddleware;

	/// <summary>
	/// Adds middleware using a factory function to the pipeline.
	/// </summary>
	/// <param name="middlewareFactory"> The middleware factory function. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder Use(Func<IServiceProvider, IDispatchMiddleware> middlewareFactory);

	/// <summary>
	/// Adds middleware of the specified type at a specific stage in the pipeline.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <param name="stage"> The stage where the middleware should be inserted. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder UseAt<TMiddleware>(DispatchMiddlewareStage stage)
		where TMiddleware : IDispatchMiddleware;

	/// <summary>
	/// Conditionally adds middleware of the specified type to the pipeline.
	/// </summary>
	/// <typeparam name="TMiddleware"> The middleware type. </typeparam>
	/// <param name="condition"> The condition function that determines if middleware should be added. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder UseWhen<TMiddleware>(Func<IServiceProvider, bool> condition)
		where TMiddleware : IDispatchMiddleware;

	/// <summary>
	/// Configures which message kinds this pipeline should process.
	/// </summary>
	/// <param name="messageKinds"> The message kinds to process. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder ForMessageKinds(MessageKinds messageKinds);

	/// <summary>
	/// Uses a named pipeline profile with predefined middleware configuration.
	/// </summary>
	/// <param name="profileName"> The name of the profile to use. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder UseProfile(string profileName);

	/// <summary>
	/// Uses a pipeline profile with predefined middleware configuration.
	/// </summary>
	/// <param name="profile"> The profile to use. </param>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder UseProfile(IPipelineProfile profile);

	/// <summary>
	/// Clears all configured middleware from the pipeline.
	/// </summary>
	/// <returns> The pipeline builder for method chaining. </returns>
	IPipelineBuilder Clear();

	/// <summary>
	/// Builds the configured pipeline.
	/// </summary>
	/// <returns> The built dispatch pipeline. </returns>
	IDispatchPipeline Build();
}
