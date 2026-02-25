// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Cached delegate factory for message handlers.
/// </summary>
/// <typeparam name="TMessage"> The message type handled by the cached delegates. </typeparam>
public sealed class MessageHandlerDelegateCache<TMessage>
{
	private readonly ConcurrentDictionary<Type, Func<TMessage, Task>> _asyncHandlers = new();
	private readonly ConcurrentDictionary<Type, Func<TMessage, ValueTask>> _valueTaskHandlers = new();
	private readonly ConcurrentDictionary<Type, Action<TMessage>> _syncHandlers = new();

	/// <summary>
	/// Gets or creates an async message handler delegate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Func<TMessage, Task> GetAsyncHandler(Type handlerType, Func<Func<TMessage, Task>> factory) =>
		_asyncHandlers.GetOrAdd(handlerType, (_, f) => f(), factory);

	/// <summary>
	/// Gets or creates a value task message handler delegate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Func<TMessage, ValueTask> GetValueTaskHandler(Type handlerType, Func<Func<TMessage, ValueTask>> factory) =>
		_valueTaskHandlers.GetOrAdd(handlerType, (_, f) => f(), factory);

	/// <summary>
	/// Gets or creates a synchronous message handler delegate.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Action<TMessage> GetSyncHandler(Type handlerType, Func<Action<TMessage>> factory) =>
		_syncHandlers.GetOrAdd(handlerType, (_, f) => f(), factory);

	/// <summary>
	/// Clears all cached handlers.
	/// </summary>
	public void Clear()
	{
		_asyncHandlers.Clear();
		_valueTaskHandlers.Clear();
		_syncHandlers.Clear();
	}
}
