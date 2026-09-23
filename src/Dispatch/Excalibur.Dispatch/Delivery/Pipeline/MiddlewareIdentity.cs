// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Answers "which middleware is this entry?" for a composed pipeline, where an entry may be the
/// middleware itself, a decorator around it, or a resolver that produces one per dispatch.
/// </summary>
/// <remarks>
/// A pipeline entry's runtime type stopped being a reliable answer once entries could stand in for
/// middleware rather than be it. <see cref="ScopeResolvedMiddleware"/> holds no instance at all -- that
/// is the point of it -- so a caller that reads <c>GetType()</c> or tests <c>is TMiddleware</c> sees the
/// stand-in and not the middleware, which silently changes ordering signatures and bypass decisions.
/// </remarks>
internal static class MiddlewareIdentity
{
	/// <summary>
	/// Returns the type of the middleware an entry stands for: the type it resolves per dispatch, the
	/// type it decorates, or its own.
	/// </summary>
	/// <param name="middleware"> The pipeline entry. </param>
	/// <returns> The middleware type this entry represents. </returns>
	public static Type TypeOf(IDispatchMiddleware middleware)
	{
		ArgumentNullException.ThrowIfNull(middleware);

		var current = middleware;
		while (true)
		{
			switch (current)
			{
				case IMiddlewareTypeIdentity identity:
					return identity.MiddlewareType;

				case IMiddlewareDecorator decorator:
					current = decorator.Inner;
					continue;

				default:
					return current.GetType();
			}
		}
	}

	/// <summary>
	/// Returns whether an entry stands for <see cref="RoutingMiddleware"/>.
	/// </summary>
	/// <param name="middleware"> The pipeline entry. </param>
	/// <returns> <see langword="true"/> when the entry represents routing; otherwise <see langword="false"/>. </returns>
	/// <remarks>
	/// The dispatcher pre-routes local actions, so a chain that carries nothing but routing can be skipped
	/// entirely. Reading the type through <see cref="TypeOf"/> keeps that fast path available for a routing
	/// middleware the container serves per scope, which would otherwise read as some other middleware and
	/// force the chain to execute for no effect.
	/// </remarks>
	public static bool IsRouting(IDispatchMiddleware middleware) => TypeOf(middleware) == typeof(RoutingMiddleware);
}
