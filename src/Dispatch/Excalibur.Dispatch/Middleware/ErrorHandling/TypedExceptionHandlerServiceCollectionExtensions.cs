// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.ErrorHandling;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering typed exception handler services.
/// </summary>
public static class TypedExceptionHandlerServiceCollectionExtensions
{
	/// <summary>
	/// Adds typed exception handler middleware and registers the specified handler.
	/// </summary>
	/// <typeparam name="TException">The exception type to handle.</typeparam>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddTypedExceptionHandler<TException, THandler>(
		this IServiceCollection services)
		where TException : Exception
		where THandler : class, ITypedExceptionHandler<TException>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<TypedExceptionHandlerMiddleware>();
		services.AddSingleton<ITypedExceptionHandler<TException>, THandler>();

		return services;
	}

	/// <summary>
	/// Adds the typed exception handler middleware without registering specific handlers.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this method when handlers are registered separately or via assembly scanning.
	/// </remarks>
	public static IServiceCollection AddTypedExceptionHandlerMiddleware(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<TypedExceptionHandlerMiddleware>();

		return services;
	}
}
