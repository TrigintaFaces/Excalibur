// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Caches typed dispatch delegates for convenience overloads that infer <c>TResponse</c>
/// from <see cref="IDispatchAction{TResponse}"/> parameter types. Each unique message type
/// incurs a one-time <see cref="MethodInfo.MakeGenericMethod"/> cost; subsequent calls use
/// the cached delegate with zero reflection overhead.
/// </summary>
/// <typeparam name="TResponse">The response type inferred from the action interface.</typeparam>
/// <remarks>
/// <para>
/// This cache exists as the non-AOT fallback for the convenience <c>DispatchAsync</c> overloads.
/// When the <c>Excalibur.Dispatch.SourceGenerators</c> package is referenced, the source generator
/// emits concrete typed extension methods that shadow the fallback overloads via C# overload
/// resolution (concrete parameter type is more specific than interface parameter type).
/// </para>
/// <para>
/// Thread safety: <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
/// may invoke the factory concurrently for the same key; all delegate factories are pure and
/// idempotent, so duplicate work is harmless.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Uses MakeGenericMethod for typed dispatch delegate creation. " +
                           "Use source-generated typed dispatch overloads for AOT/trimming compatibility.")]
[RequiresDynamicCode("Uses MakeGenericMethod which requires runtime code generation. " +
                     "Use source-generated typed dispatch overloads for AOT/trimming compatibility.")]
internal static class TypedDispatchDelegateCache<TResponse>
{
	private static readonly ConcurrentDictionary<Type, Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>>
		DispatchDelegates = new();

	private static readonly ConcurrentDictionary<Type, Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>>
		DispatchChildDelegates = new();

	private static readonly ConcurrentDictionary<Type, Func<IDispatcher, IDispatchAction<TResponse>, IMessageContext, CancellationToken, Task<IMessageResult<TResponse>>>>
		DispatchWithContextDelegates = new();

	private static readonly MethodInfo DispatchMethod =
		typeof(TypedDispatchDelegateCache<TResponse>).GetMethod(
			nameof(InvokeDispatch), BindingFlags.NonPublic | BindingFlags.Static)!;

	private static readonly MethodInfo DispatchChildMethod =
		typeof(TypedDispatchDelegateCache<TResponse>).GetMethod(
			nameof(InvokeDispatchChild), BindingFlags.NonPublic | BindingFlags.Static)!;

	private static readonly MethodInfo DispatchWithContextMethod =
		typeof(TypedDispatchDelegateCache<TResponse>).GetMethod(
			nameof(InvokeDispatchWithContext), BindingFlags.NonPublic | BindingFlags.Static)!;

	/// <summary>
	/// Gets or creates a cached delegate for <see cref="DispatcherContextExtensions.DispatchAsync{TMessage,TResponse}(IDispatcher,TMessage,CancellationToken)"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>
		GetDispatchDelegate(Type messageType)
	{
		return DispatchDelegates.GetOrAdd(messageType, static type =>
			CreateDelegate<Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>>(
				DispatchMethod, type));
	}

	/// <summary>
	/// Gets or creates a cached delegate for <see cref="DispatcherContextExtensions.DispatchChildAsync{TMessage,TResponse}(IDispatcher,TMessage,CancellationToken)"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>
		GetDispatchChildDelegate(Type messageType)
	{
		return DispatchChildDelegates.GetOrAdd(messageType, static type =>
			CreateDelegate<Func<IDispatcher, IDispatchAction<TResponse>, CancellationToken, Task<IMessageResult<TResponse>>>>(
				DispatchChildMethod, type));
	}

	/// <summary>
	/// Gets or creates a cached delegate for <see cref="IDispatcher.DispatchAsync{TMessage,TResponse}(TMessage,IMessageContext,CancellationToken)"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static Func<IDispatcher, IDispatchAction<TResponse>, IMessageContext, CancellationToken, Task<IMessageResult<TResponse>>>
		GetDispatchWithContextDelegate(Type messageType)
	{
		return DispatchWithContextDelegates.GetOrAdd(messageType, static type =>
			CreateDelegate<Func<IDispatcher, IDispatchAction<TResponse>, IMessageContext, CancellationToken, Task<IMessageResult<TResponse>>>>(
				DispatchWithContextMethod, type));
	}

	private static TDelegate CreateDelegate<TDelegate>(MethodInfo openMethod, Type messageType)
		where TDelegate : Delegate
	{
		var closedMethod = openMethod.MakeGenericMethod(messageType);
		return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), closedMethod);
	}

	// --- Typed forwarders (one per overload variant) ---
	// These are resolved via MakeGenericMethod, then cached as delegates.
	// The cast (TMessage)message is safe: the compiler enforces IDispatchAction<TResponse>
	// at the call site, and TMessage : IDispatchAction<TResponse> by constraint.

	private static Task<IMessageResult<TResponse>> InvokeDispatch<TMessage>(
		IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		return DispatcherContextExtensions.DispatchAsync<TMessage, TResponse>(
			dispatcher, (TMessage)message, cancellationToken);
	}

	private static Task<IMessageResult<TResponse>> InvokeDispatchChild<TMessage>(
		IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		return DispatcherContextExtensions.DispatchChildAsync<TMessage, TResponse>(
			dispatcher, (TMessage)message, cancellationToken);
	}

	private static Task<IMessageResult<TResponse>> InvokeDispatchWithContext<TMessage>(
		IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		IMessageContext context,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		return dispatcher.DispatchAsync<TMessage, TResponse>(
			(TMessage)message, context, cancellationToken);
	}
}
