// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Represents a middleware registration in the dispatch pipeline.
/// </summary>
/// <remarks> Creates a new middleware registration. </remarks>
/// <param name="middlewareType"> The type of the middleware. </param>
/// <param name="stage"> The pipeline stage where the middleware should be registered. </param>
/// <param name="order"> The execution order within the stage. </param>
/// <param name="configureOptions"> Optional service configuration delegate. </param>
public sealed class MiddlewareRegistration(
	Type middlewareType,
	DispatchMiddlewareStage stage,
	int order = 100,
	Action<IServiceCollection>? configureOptions = null)
{
	/// <summary>
	/// Gets the type of the middleware.
	/// </summary>
	/// <value> The middleware implementation type to register. </value>
	public Type MiddlewareType { get; } = middlewareType ?? throw new ArgumentNullException(nameof(middlewareType));

	/// <summary>
	/// Gets the pipeline stage where the middleware should be registered.
	/// </summary>
	/// <value> The dispatch middleware stage targeted by the registration. </value>
	public DispatchMiddlewareStage Stage { get; } = stage;

	/// <summary>
	/// Gets or sets the execution order within the stage.
	/// </summary>
	/// <value> The ordering applied among middleware within the same stage. </value>
	public int Order { get; set; } = order;

	/// <summary>
	/// Gets or sets a value indicating whether this middleware is enabled.
	/// </summary>
	/// <value> <see langword="true" /> when the middleware is active; otherwise, <see langword="false" />. </value>
	public bool IsEnabled { get; set; } = true;

	/// <summary>
	/// Gets the optional service configuration delegate.
	/// </summary>
	/// <value> The optional service configuration delegate supplied with the registration. </value>
	public Action<IServiceCollection>? ConfigureOptions { get; } = configureOptions;
}
