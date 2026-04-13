// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides thread-local storage for the current message context.
/// Uses a dual-layer approach: ThreadStatic for synchronous fast paths (zero allocation)
/// and AsyncLocal for async continuations that need context flow across awaits.
/// </summary>
public static class MessageContextHolder
{
	private static readonly AsyncLocal<IMessageContext?> _current = new();

	// PERF: ThreadStatic overlay for synchronous dispatch fast paths.
	// When the sync layer is active, the getter returns s_syncCurrent instead of reading AsyncLocal.
	// This allows the Dispatcher to push/pop context without any AsyncLocal writes (~72B savings)
	// on the synchronous completion path (the common case for in-process handlers).
	[ThreadStatic] private static IMessageContext? s_syncCurrent;
	[ThreadStatic] private static bool s_syncLayerActive;

	/// <summary>
	/// Gets or sets the current message context.
	/// The getter prefers the ThreadStatic sync layer when active; otherwise reads AsyncLocal.
	/// The setter always writes to AsyncLocal (used by middleware and async dispatch paths).
	/// </summary>
	/// <value>
	/// The current message context.
	/// </value>
	public static IMessageContext? Current
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => s_syncLayerActive ? s_syncCurrent : _current.Value;
		set => _current.Value = value;
	}

	/// <summary>
	/// Clears the current message context from both layers.
	/// </summary>
	public static void Clear()
	{
		s_syncCurrent = null;
		s_syncLayerActive = false;
		_current.Value = null;
	}

	/// <summary>
	/// Pushes context onto the synchronous ThreadStatic layer only (zero allocation).
	/// The handler can read <see cref="Current"/> during synchronous execution and will
	/// see the correct value via the ThreadStatic fast path.
	/// </summary>
	/// <param name="context">The context to make current.</param>
	/// <returns>The previous context for restoration via <see cref="PopSync"/>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IMessageContext? PushSync(IMessageContext context)
	{
		var previous = s_syncLayerActive ? s_syncCurrent : _current.Value;
		s_syncCurrent = context;
		s_syncLayerActive = true;
		return previous;
	}

	/// <summary>
	/// Restores the synchronous ThreadStatic layer to its previous state (zero allocation).
	/// </summary>
	/// <param name="previous">The previous context returned by <see cref="PushSync"/>.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void PopSync(IMessageContext? previous)
	{
		s_syncCurrent = previous;
		s_syncLayerActive = previous is not null;
	}

	/// <summary>
	/// Promotes the current ThreadStatic context to AsyncLocal and deactivates the sync layer.
	/// Call this when a dispatch that started synchronously transitions to async, so that
	/// async continuations can see the ambient context via AsyncLocal flow.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void PromoteToAsyncAndPop(IMessageContext? syncPrevious)
	{
		// Deactivate sync layer.
		s_syncCurrent = null;
		s_syncLayerActive = false;
		// Write the sync-previous value to AsyncLocal so PopAmbientContext restores correctly.
		// Note: On the async path, the caller already holds the context reference and will
		// pass it to AwaitDirectLocal*Async. The AsyncLocal write here is acceptable because
		// this is the slow (async) path.
		// We don't need to push the current context to AsyncLocal because the handler's
		// continuation already captured its EC before we could set AsyncLocal.
		// Just restore to the previous state so subsequent Current reads return the right value.
		_current.Value = syncPrevious;
	}
}
