// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Gets dispatch metadata for an action type from a precompiled direct-action dispatch table.
/// </summary>
/// <remarks>
/// A custom delegate rather than <see cref="Func{T,TResult}"/> because of the <c>out</c> parameters,
/// which <see cref="Func{T,TResult}"/> cannot express.
/// </remarks>
public delegate bool PrecompiledDirectTryGetMetadata(Type actionType, out bool expectsResponse, out bool requiresContext);

/// <summary>
/// AOT-safe registry of precompiled direct-action dispatch tables.
/// Each consuming assembly's source-generated <c>PrecompiledDirectActionDispatch</c> class registers
/// itself here via a <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>, at module
/// load time. The framework's local message bus consults this registry instead of scanning
/// <see cref="AppDomain.CurrentDomain"/> for a well-known type name via reflection: a self-registering
/// module initializer is visible to the trimmer (the call site is ordinary code), where a string-named
/// <c>Assembly.GetType</c> lookup is not.
/// </summary>
/// <remarks>
/// Public — not a convenience choice. The generated <c>PrecompiledDirectActionDispatch</c> class this
/// registers is compiled into the CONSUMER's own assembly (whichever package references the source
/// generator), so <see cref="Register"/> is called across an assembly boundary the framework cannot
/// name in advance. <c>InternalsVisibleTo</c> requires listing every friend assembly at compile time,
/// which is impossible for an unbounded set of NuGet consumers — public is the only mechanism available.
/// </remarks>
public static class PrecompiledDirectDispatchRegistry
{
	private static readonly ConcurrentQueue<(
		Func<Type, bool> CanHandle,
		PrecompiledDirectTryGetMetadata TryGetMetadata,
		Func<IDispatchAction, IServiceProvider, IMessageContext?, CancellationToken, ValueTask<object?>> Invoke)> _providers = new();

	/// <summary>
	/// Projects an already-successfully-completed <see cref="Task{TResult}"/> onto a
	/// <see cref="ValueTask{TResult}"/> of <see cref="object"/> without allocating an async state machine.
	/// </summary>
	/// <remarks>
	/// Public for the same reason as <see cref="Register"/>: the only caller is the generated
	/// <c>PrecompiledDirectActionDispatch</c> class compiled into the consumer's own assembly.
	/// <para>
	/// The caller MUST have observed <see cref="Task.IsCompletedSuccessfully"/> first. This is a
	/// completed-result read, not a wait: it cannot block, and it cannot observe a fault or a
	/// cancellation, because neither state satisfies that precondition. Keeping it here rather than in
	/// emitted source means the single justified result-read lives in framework source that is reviewed,
	/// instead of being re-emitted into every consuming assembly where it reads as sync-over-async.
	/// </para>
	/// </remarks>
	/// <typeparam name="TResult">The task's result type.</typeparam>
	/// <param name="task">A task already observed to be successfully completed.</param>
	/// <returns>A completed <see cref="ValueTask{TResult}"/> carrying the task's result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><paramref name="task"/> is not successfully completed.</exception>
	public static ValueTask<object?> FromCompletedResult<TResult>(Task<TResult> task)
	{
		ArgumentNullException.ThrowIfNull(task);

		if (!task.IsCompletedSuccessfully)
		{
			throw new InvalidOperationException(
				"FromCompletedResult requires a successfully completed task; await the task instead.");
		}

#pragma warning disable RS0030, CA1849 // Guarded by IsCompletedSuccessfully above: a result read, never a wait.
		return new ValueTask<object?>(task.Result);
#pragma warning restore RS0030, CA1849
	}

	/// <summary>
	/// Registers a precompiled direct-action dispatch table. Called by source-generated module initializers.
	/// </summary>
	public static void Register(
		Func<Type, bool> canHandle,
		PrecompiledDirectTryGetMetadata tryGetMetadata,
		Func<IDispatchAction, IServiceProvider, IMessageContext?, CancellationToken, ValueTask<object?>> invoke)
	{
		ArgumentNullException.ThrowIfNull(canHandle);
		ArgumentNullException.ThrowIfNull(tryGetMetadata);
		ArgumentNullException.ThrowIfNull(invoke);
		_providers.Enqueue((canHandle, tryGetMetadata, invoke));
	}

	/// <summary>
	/// Returns all registered precompiled direct-action dispatch tables, in registration order.
	/// Internal: the only consumer is the framework's local message bus, in this same assembly.
	/// </summary>
	internal static (
		Func<Type, bool> CanHandle,
		PrecompiledDirectTryGetMetadata TryGetMetadata,
		Func<IDispatchAction, IServiceProvider, IMessageContext?, CancellationToken, ValueTask<object?>> Invoke)[] GetAll() => [.. _providers];
}
