// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Implemented by middleware that wraps another middleware, so that callers reasoning about middleware
/// identity can reach the middleware actually being decorated.
/// </summary>
/// <remarks>
/// A decorator's own type says nothing about which middleware it carries: two unrelated registrations
/// wrapped by the same decorator share a runtime type. Anything that groups, counts or compares middleware
/// by type must therefore compare the decorated middleware, not the wrapper. Every decorator implements
/// this interface so those callers keep working unchanged when a new decorator is introduced.
/// </remarks>
internal interface IMiddlewareDecorator
{
	/// <summary>
	/// Gets the middleware this instance decorates.
	/// </summary>
	IDispatchMiddleware Inner { get; }
}

/// <summary>
/// Identity helpers for middleware that may be wrapped by one or more decorators.
/// </summary>
internal static class MiddlewareDecoratorExtensions
{
	/// <summary>
	/// Returns the innermost decorated middleware, unwrapping any number of nested decorators.
	/// </summary>
	/// <param name="middleware"> The middleware to unwrap. </param>
	/// <returns> The innermost middleware, or <paramref name="middleware" /> when it is not a decorator. </returns>
	internal static IDispatchMiddleware Unwrap(this IDispatchMiddleware middleware)
	{
		ArgumentNullException.ThrowIfNull(middleware);

		var current = middleware;
		while (current is IMiddlewareDecorator decorator)
		{
			current = decorator.Inner;
		}

		return current;
	}

	/// <summary>
	/// Returns the runtime type of the innermost decorated middleware.
	/// </summary>
	/// <param name="middleware"> The middleware to unwrap. </param>
	/// <returns> The type of the middleware being decorated. </returns>
	internal static Type UnwrappedType(this IDispatchMiddleware middleware) => middleware.Unwrap().GetType();
}
