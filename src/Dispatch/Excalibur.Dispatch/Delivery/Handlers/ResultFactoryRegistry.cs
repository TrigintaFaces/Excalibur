// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Routing;

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
	private static readonly ConcurrentDictionary<Type, Func<object?, RoutingDecision?, object?, IAuthorizationResult?, MessageDisposition, IMessageResult>> _factories = new();

	private static readonly ConcurrentDictionary<Type, Func<object?, MessageDisposition, IMessageResult>> _leanFactories = new();

	/// <summary>
	/// Registers a factory for creating <c>MessageResult.Success&lt;T&gt;</c> instances.
	/// </summary>
	/// <typeparam name="T">The result type.</typeparam>
	/// <remarks>
	/// Registers both the full factory (used when a routing, validation, or authorization result is
	/// present) and the lean factory used by the plain dispatch path. One call covers both, so callers
	/// and code generators never have to know which path a given dispatch will take.
	/// </remarks>
	public static void RegisterFactory<T>()
	{
		_factories.TryAdd(
			typeof(T),
			static (returnValue, routing, validation, auth, disposition) =>
				MessageResult.Success<T>(
					(T)returnValue!,
					routing,
					validation,
					auth,
					disposition));

		// Mirrors the reflective lean factory, including its null-becomes-default(T) behaviour.
		_leanFactories.TryAdd(
			typeof(T),
			static (returnValue, disposition) =>
				new SimpleSuccessMessageResultOfT<T>(
					returnValue is null ? default : (T)returnValue,
					disposition));
	}

	/// <summary>
	/// Gets a factory for creating MessageResult instances of the specified type.
	/// </summary>
	internal static Func<object?, RoutingDecision?, object?, IAuthorizationResult?, MessageDisposition, IMessageResult>? GetFactory(Type resultType)
	{
		return _factories.TryGetValue(resultType, out var factory) ? factory : null;
	}

	/// <summary>
	/// Gets a factory for creating lean success results of the specified type, used by the plain
	/// dispatch path where no routing, validation, or authorization result is present.
	/// </summary>
	internal static Func<object?, MessageDisposition, IMessageResult>? GetLeanFactory(Type resultType)
	{
		return _leanFactories.TryGetValue(resultType, out var factory) ? factory : null;
	}
}
