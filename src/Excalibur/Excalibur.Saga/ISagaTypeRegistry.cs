// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga;

/// <summary>
/// AOT-safe type registry for resolving saga and timeout message types by name
/// without runtime assembly scanning.
/// </summary>
/// <remarks>
/// <para>
/// Populated at DI registration time via <c>AddSaga&lt;T&gt;()</c> calls.
/// When registered, <see cref="Services.SagaTimeoutDeliveryService"/> uses this
/// instead of <c>AppDomain.GetAssemblies()</c> / <c>Assembly.GetType()</c>.
/// </para>
/// <para>
/// Registration happens at DI composition time only. Call <see cref="Freeze"/>
/// after build to prevent accidental runtime registration.
/// </para>
/// </remarks>
internal interface ISagaTypeRegistry
{
	/// <summary>
	/// Resolves a saga or timeout message type from its stored type name.
	/// </summary>
	/// <param name="typeName">The type name as stored in the timeout store.</param>
	/// <returns>The resolved <see cref="Type"/>, or <see langword="null"/> if not registered.</returns>
	Type? ResolveType(string typeName);

	/// <summary>
	/// Registers a type for AOT-safe resolution.
	/// </summary>
	/// <param name="type">The type to register.</param>
	/// <exception cref="InvalidOperationException">Thrown if the registry has been frozen.</exception>
	void RegisterType(Type type);

	/// <summary>
	/// Freezes the registry, preventing further registrations.
	/// Called after DI composition is complete.
	/// </summary>
	void Freeze();
}

/// <summary>
/// Default implementation of <see cref="ISagaTypeRegistry"/> backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> for defense-in-depth thread safety.
/// </summary>
/// <remarks>
/// Registration contract: mutation at DI composition time only; lookups at runtime.
/// Use <see cref="Freeze"/> after build to enforce this invariant.
/// </remarks>
internal sealed class SagaTypeRegistry : ISagaTypeRegistry
{
	private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);
	private volatile bool _frozen;

	/// <inheritdoc />
	public Type? ResolveType(string typeName)
	{
		if (_types.TryGetValue(typeName, out var type))
		{
			return type;
		}

		// Try simple name (without assembly qualifier)
		var commaIndex = typeName.IndexOf(',', StringComparison.Ordinal);
		if (commaIndex > 0)
		{
			var simpleName = typeName[..commaIndex].Trim();
			if (_types.TryGetValue(simpleName, out type))
			{
				return type;
			}
		}

		return null;
	}

	/// <inheritdoc />
	public void RegisterType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		if (_frozen)
		{
			throw new InvalidOperationException(
				$"Cannot register type '{type.FullName}' after the saga type registry has been frozen. " +
				"All saga types must be registered during DI composition.");
		}

		if (type.FullName is not null)
		{
			_types[type.FullName] = type;
		}

		if (type.AssemblyQualifiedName is not null)
		{
			_types[type.AssemblyQualifiedName] = type;
		}
	}

	/// <inheritdoc />
	public void Freeze() => _frozen = true;
}

/// <summary>
/// AOT-safe dispatch registry for invoking <c>HandleEventInternalAsync&lt;TSaga, TSagaState&gt;</c>
/// without <see cref="System.Reflection.MethodInfo.MakeGenericMethod"/>.
/// </summary>
/// <remarks>
/// <para>
/// Populated at DI registration time via <c>AddSaga&lt;TSaga, TSagaState&gt;()</c>.
/// When registered, <see cref="Orchestration.SagaCoordinator"/> uses the typed delegate
/// instead of runtime generic method construction.
/// </para>
/// <para>
/// Registration happens at DI composition time only. Call <see cref="Freeze"/>
/// after build to prevent accidental runtime registration.
/// </para>
/// </remarks>
internal interface ISagaDispatchRegistry
{
	/// <summary>
	/// Retrieves a typed dispatch delegate for the given saga and state types.
	/// </summary>
	/// <param name="sagaType">The saga type.</param>
	/// <param name="stateType">The saga state type.</param>
	/// <returns>The dispatch delegate, or <see langword="null"/> if not registered.</returns>
	Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task>? GetDispatcher(Type sagaType, Type stateType);

	/// <summary>
	/// Registers a typed dispatch delegate for a saga type.
	/// </summary>
	/// <param name="sagaType">The saga type.</param>
	/// <param name="stateType">The saga state type.</param>
	/// <param name="dispatcher">The typed dispatch delegate.</param>
	/// <exception cref="InvalidOperationException">Thrown if the registry has been frozen.</exception>
	void Register(Type sagaType, Type stateType, Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> dispatcher);

	/// <summary>
	/// Freezes the registry, preventing further registrations.
	/// Called after DI composition is complete.
	/// </summary>
	void Freeze();
}

/// <summary>
/// Default implementation of <see cref="ISagaDispatchRegistry"/> backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> for defense-in-depth thread safety.
/// </summary>
/// <remarks>
/// Registration contract: mutation at DI composition time only; lookups at runtime.
/// Use <see cref="Freeze"/> after build to enforce this invariant.
/// </remarks>
internal sealed class SagaDispatchRegistry : ISagaDispatchRegistry
{
	private readonly ConcurrentDictionary<(Type, Type), Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task>> _dispatchers = new();
	private volatile bool _frozen;

	/// <inheritdoc />
	public Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task>? GetDispatcher(Type sagaType, Type stateType)
	{
		return _dispatchers.GetValueOrDefault((sagaType, stateType));
	}

	/// <inheritdoc />
	public void Register(Type sagaType, Type stateType, Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> dispatcher)
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		if (_frozen)
		{
			throw new InvalidOperationException(
				$"Cannot register dispatch for saga '{sagaType.Name}/{stateType.Name}' after the saga dispatch registry has been frozen. " +
				"All saga dispatchers must be registered during DI composition.");
		}

		_dispatchers[(sagaType, stateType)] = dispatcher;
	}

	/// <inheritdoc />
	public void Freeze() => _frozen = true;
}
