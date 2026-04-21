// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// AOT-safe static registry for creating <see cref="SagaContext{TData, TMessage}"/> instances
/// without <see cref="Type.MakeGenericType"/> or <see cref="System.Activator.CreateInstance(Type, object[])"/>.
/// </summary>
/// <remarks>
/// <para>
/// Populated at DI registration time via <c>AddSaga&lt;TSaga, TSagaState&gt;()</c> calls.
/// When registered, <see cref="ProcessManager{TData}"/> uses the factory delegate
/// instead of runtime generic type construction.
/// </para>
/// <para>
/// Registration happens at DI composition time only. Call <see cref="Freeze"/>
/// after build to prevent accidental runtime registration.
/// </para>
/// </remarks>
internal static class SagaContextFactoryRegistry
{
	/// <summary>
	/// Factory delegate: (data, message, processManager) → SagaContext{TData, TMessage}.
	/// </summary>
	private static readonly ConcurrentDictionary<(Type DataType, Type MessageType), Func<object, object, object, object>> Factories = new();

	private static volatile bool _frozen;

	/// <summary>
	/// Creates a <see cref="SagaContext{TData, TMessage}"/> for the given type pair.
	/// </summary>
	/// <returns>The context object, or <see langword="null"/> if no factory is registered.</returns>
	internal static object? CreateContext(Type dataType, Type messageType, object data, object message, object processManager)
	{
		return Factories.TryGetValue((dataType, messageType), out var factory)
			? factory(data, message, processManager)
			: null;
	}

	/// <summary>
	/// Registers a context factory for a (TData, TMessage) type pair.
	/// </summary>
	/// <typeparam name="TData">The saga state data type.</typeparam>
	/// <typeparam name="TMessage">The message type.</typeparam>
	internal static void Register<TData, TMessage>()
		where TData : SagaState
		where TMessage : class
	{
		if (_frozen)
		{
			throw new InvalidOperationException(
				$"Cannot register context factory for ({typeof(TData).Name}, {typeof(TMessage).Name}) " +
				"after the registry has been frozen.");
		}

		Factories[(typeof(TData), typeof(TMessage))] = static (data, message, pm) =>
			new SagaContext<TData, TMessage>((TData)data, (TMessage)message, (ProcessManager<TData>)pm);
	}

	/// <summary>
	/// Freezes the registry, preventing further registrations.
	/// </summary>
	internal static void Freeze() => _frozen = true;

	/// <summary>
	/// Clears the registry. Intended for testing only.
	/// </summary>
	internal static void Clear()
	{
		_frozen = false;
		Factories.Clear();
	}
}
