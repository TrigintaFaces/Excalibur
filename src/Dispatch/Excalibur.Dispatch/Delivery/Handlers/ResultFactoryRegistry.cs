// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Registry for AOT-compatible result factory methods.
/// </summary>
/// <remarks>
/// <para>
/// In AOT mode, <see cref="FinalDispatchHandler"/> cannot use <c>MakeGenericMethod</c>
/// to create <c>MessageResult.Success&lt;T&gt;</c>. This registry provides pre-registered
/// factory delegates for known result types.
/// </para>
/// <para>
/// Factory registrations are populated at startup by source-generated
/// <c>[ModuleInitializer]</c> code that calls <see cref="RegisterFactory{T}"/>.
/// </para>
/// </remarks>
public static partial class ResultFactoryRegistry
{
	private static readonly ConcurrentDictionary<Type, Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>> _factories = new();

	/// <summary>
	/// Registers a factory for creating <c>MessageResult.Success&lt;T&gt;</c> instances.
	/// </summary>
	/// <typeparam name="T">The result type.</typeparam>
	public static void RegisterFactory<T>()
	{
		_factories.TryAdd(
			typeof(T),
			static (returnValue, routing, validation, auth, cacheHit) =>
				MessageResult.Success<T>(
					(T)returnValue!,
					routing,
					validation,
					auth,
					cacheHit));
	}

	/// <summary>
	/// Gets a factory for creating MessageResult instances of the specified type.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Factory delegates are registered at startup by source-generated code. No reflection at runtime.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Factory delegates are registered at startup by source-generated code. No runtime code generation.")]
	internal static Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>? GetFactory(Type resultType)
	{
		return _factories.TryGetValue(resultType, out var factory) ? factory : null;
	}
}
