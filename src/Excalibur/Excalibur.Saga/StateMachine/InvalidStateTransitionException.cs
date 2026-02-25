// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Exception thrown when an invalid state transition is attempted in a process manager.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when:
/// </para>
/// <list type="bullet">
/// <item><description>A transition to an undefined state is attempted</description></item>
/// <item><description>A message is received that the current state cannot handle</description></item>
/// <item><description>A transition violates state machine constraints</description></item>
/// </list>
/// <para>
/// The exception provides context about the current state, attempted transition,
/// and the message type that triggered the invalid transition attempt.
/// </para>
/// </remarks>
public sealed class InvalidStateTransitionException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class
	/// with the specified transition context.
	/// </summary>
	/// <param name="currentState">The name of the current state.</param>
	/// <param name="attemptedTransition">The name of the state that was attempted.</param>
	/// <param name="messageType">The type of message that triggered the transition, or null if not applicable.</param>
	public InvalidStateTransitionException(
		string currentState,
		string attemptedTransition,
		Type? messageType)
		: base(FormatMessage(currentState, attemptedTransition, messageType))
	{
		CurrentState = currentState;
		AttemptedTransition = attemptedTransition;
		MessageType = messageType;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class
	/// with the specified transition context and inner exception.
	/// </summary>
	/// <param name="currentState">The name of the current state.</param>
	/// <param name="attemptedTransition">The name of the state that was attempted.</param>
	/// <param name="messageType">The type of message that triggered the transition, or null if not applicable.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public InvalidStateTransitionException(
		string currentState,
		string attemptedTransition,
		Type? messageType,
		Exception innerException)
		: base(FormatMessage(currentState, attemptedTransition, messageType), innerException)
	{
		CurrentState = currentState;
		AttemptedTransition = attemptedTransition;
		MessageType = messageType;
	}

	/// <summary>
	/// Gets the name of the current state when the invalid transition was attempted.
	/// </summary>
	/// <value>The current state name.</value>
	public string CurrentState { get; }

	/// <summary>
	/// Gets the name of the state that was attempted but is not valid.
	/// </summary>
	/// <value>The attempted target state name.</value>
	public string AttemptedTransition { get; }

	/// <summary>
	/// Gets the type of message that triggered the invalid transition attempt, if applicable.
	/// </summary>
	/// <value>The message type, or null if not triggered by a specific message.</value>
	public Type? MessageType { get; }

	private static string FormatMessage(
		string currentState,
		string attemptedTransition,
		Type? messageType)
	{
		var messageInfo = messageType is not null
			? $" triggered by message type '{messageType.Name}'"
			: string.Empty;

		return $"Invalid state transition from '{currentState}' to '{attemptedTransition}'{messageInfo}. " +
			   $"The target state '{attemptedTransition}' is not defined or the transition is not allowed.";
	}
}
