// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Implementation of <see cref="IMessageHandler{TData, TMessage}"/> that builds
/// message handling behavior for process manager states.
/// </summary>
/// <typeparam name="TData">The type of saga state data that extends <see cref="SagaState"/>.</typeparam>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
internal sealed class MessageHandler<TData, TMessage> : IMessageHandler<TData, TMessage>
	where TData : SagaState
	where TMessage : class
{
	private readonly List<Action<SagaContext<TData, TMessage>>> _actions = [];
	private Func<TData, TMessage, bool>? _condition;
	private string? _targetState;
	private bool _shouldComplete;

	/// <summary>
	/// Gets the target state name for transition, if any.
	/// </summary>
	internal string? TargetState => _targetState;

	/// <summary>
	/// Gets a value indicating whether the saga should be marked as completed.
	/// </summary>
	internal bool ShouldComplete => _shouldComplete;

	/// <inheritdoc />
	public IMessageHandler<TData, TMessage> TransitionTo(string stateName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
		_targetState = stateName;
		return this;
	}

	/// <inheritdoc />
	public IMessageHandler<TData, TMessage> Then(Action<SagaContext<TData, TMessage>> action)
	{
		ArgumentNullException.ThrowIfNull(action);
		_actions.Add(action);
		return this;
	}

	/// <inheritdoc />
	public IMessageHandler<TData, TMessage> If(Func<TData, TMessage, bool> condition)
	{
		ArgumentNullException.ThrowIfNull(condition);
		_condition = condition;
		return this;
	}

	/// <inheritdoc />
	public IMessageHandler<TData, TMessage> Complete()
	{
		_shouldComplete = true;
		return this;
	}

	/// <summary>
	/// Evaluates whether this handler should execute based on the configured condition.
	/// </summary>
	/// <param name="data">The saga state data.</param>
	/// <param name="message">The message being processed.</param>
	/// <returns><see langword="true"/> if the handler should execute; otherwise, <see langword="false"/>.</returns>
	internal bool ShouldHandle(TData data, TMessage message)
	{
		return _condition is null || _condition(data, message);
	}

	/// <summary>
	/// Executes all configured actions for this handler.
	/// </summary>
	/// <param name="context">The saga context containing data, message, and process manager.</param>
	internal void ExecuteActions(SagaContext<TData, TMessage> context)
	{
		foreach (var action in _actions)
		{
			action(context);
		}
	}
}
