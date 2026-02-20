// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Implementation of <see cref="IStateDefinition{TData}"/> that defines a state
/// in a process manager state machine.
/// </summary>
/// <typeparam name="TData">The type of saga state data that extends <see cref="SagaState"/>.</typeparam>
internal sealed class StateDefinition<TData> : IStateDefinition<TData>
	where TData : SagaState
{
	private readonly Dictionary<Type, object> _messageHandlers = [];
	private Action<TData>? _onEnter;
	private Action<TData>? _onExit;

	/// <summary>
	/// Initializes a new instance of the <see cref="StateDefinition{TData}"/> class.
	/// </summary>
	/// <param name="name">The name of this state.</param>
	public StateDefinition(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		Name = name;
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public IStateDefinition<TData> When<TMessage>(Action<IMessageHandler<TData, TMessage>> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(configure);

		var handler = new MessageHandler<TData, TMessage>();
		configure(handler);
		_messageHandlers[typeof(TMessage)] = handler;

		return this;
	}

	/// <inheritdoc />
	public IStateDefinition<TData> OnEnter(Action<TData> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_onEnter = action;
		return this;
	}

	/// <inheritdoc />
	public IStateDefinition<TData> OnExit(Action<TData> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_onExit = action;
		return this;
	}

	/// <summary>
	/// Invokes the OnEnter action if configured.
	/// </summary>
	/// <param name="data">The saga state data.</param>
	internal void InvokeOnEnter(TData data)
	{
		_onEnter?.Invoke(data);
	}

	/// <summary>
	/// Invokes the OnExit action if configured.
	/// </summary>
	/// <param name="data">The saga state data.</param>
	internal void InvokeOnExit(TData data)
	{
		_onExit?.Invoke(data);
	}

	/// <summary>
	/// Gets the message handler for the specified message type.
	/// </summary>
	/// <typeparam name="TMessage">The type of message.</typeparam>
	/// <returns>The message handler, or null if no handler is registered for the message type.</returns>
	internal MessageHandler<TData, TMessage>? GetHandler<TMessage>()
		where TMessage : class
	{
		return _messageHandlers.TryGetValue(typeof(TMessage), out var handler)
			? handler as MessageHandler<TData, TMessage>
			: null;
	}

	/// <summary>
	/// Attempts to get a handler for the specified message type dynamically.
	/// </summary>
	/// <param name="messageType">The type of message.</param>
	/// <returns>The handler object, or null if no handler is registered.</returns>
	internal object? GetHandlerForType(Type messageType)
	{
		return _messageHandlers.TryGetValue(messageType, out var handler) ? handler : null;
	}

	/// <summary>
	/// Gets whether this state has a handler for the specified message type.
	/// </summary>
	/// <param name="messageType">The type of message to check.</param>
	/// <returns><see langword="true"/> if a handler exists; otherwise, <see langword="false"/>.</returns>
	internal bool HasHandler(Type messageType)
	{
		return _messageHandlers.ContainsKey(messageType);
	}
}
