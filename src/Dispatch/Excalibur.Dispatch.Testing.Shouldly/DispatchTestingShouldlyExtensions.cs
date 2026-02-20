// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Testing.Tracking;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

using Shouldly;

namespace Excalibur.Dispatch.Testing;

/// <summary>
/// Shouldly assertion extensions for Excalibur Dispatch testing types.
/// Provides fluent assertions for <see cref="IDispatchedMessageLog"/>,
/// <see cref="InMemoryTransportSender"/>, <see cref="InMemoryTransportReceiver"/>,
/// <see cref="InMemoryTransportSubscriber"/>, <see cref="ISaga"/>,
/// and <see cref="DispatchTestHarness"/>.
/// </summary>
public static class DispatchTestingShouldlyExtensions
{
	#region IDispatchedMessageLog Assertions

	/// <summary>
	/// Asserts that at least one message of type <typeparamref name="TMessage"/> was dispatched.
	/// </summary>
	/// <typeparam name="TMessage">The expected message type.</typeparam>
	/// <param name="log">The dispatched message log.</param>
	public static void ShouldHaveDispatched<TMessage>(this IDispatchedMessageLog log)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(log);
		log.Any<TMessage>().ShouldBeTrue($"Expected at least one {typeof(TMessage).Name} to be dispatched, but none were found.");
	}

	/// <summary>
	/// Asserts that exactly <paramref name="expectedCount"/> messages of type
	/// <typeparamref name="TMessage"/> were dispatched.
	/// </summary>
	/// <typeparam name="TMessage">The expected message type.</typeparam>
	/// <param name="log">The dispatched message log.</param>
	/// <param name="expectedCount">The expected number of dispatched messages of this type.</param>
	public static void ShouldHaveDispatched<TMessage>(this IDispatchedMessageLog log, int expectedCount)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(log);
		log.Select<TMessage>().Count.ShouldBe(expectedCount,
			$"Expected {expectedCount} {typeof(TMessage).Name} message(s) to be dispatched.");
	}

	/// <summary>
	/// Asserts that no messages of type <typeparamref name="TMessage"/> were dispatched.
	/// </summary>
	/// <typeparam name="TMessage">The message type that should not have been dispatched.</typeparam>
	/// <param name="log">The dispatched message log.</param>
	public static void ShouldNotHaveDispatched<TMessage>(this IDispatchedMessageLog log)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(log);
		log.Any<TMessage>().ShouldBeFalse($"Expected no {typeof(TMessage).Name} to be dispatched, but found {log.Select<TMessage>().Count}.");
	}

	/// <summary>
	/// Asserts that the total number of dispatched messages equals <paramref name="expectedTotal"/>.
	/// </summary>
	/// <param name="log">The dispatched message log.</param>
	/// <param name="expectedTotal">The expected total message count.</param>
	public static void ShouldHaveDispatchedCount(this IDispatchedMessageLog log, int expectedTotal)
	{
		ArgumentNullException.ThrowIfNull(log);
		log.Count.ShouldBe(expectedTotal, "Unexpected total dispatched message count.");
	}

	#endregion IDispatchedMessageLog Assertions

	#region Transport Sender Assertions

	/// <summary>
	/// Asserts that exactly <paramref name="expectedCount"/> messages were sent through the sender.
	/// </summary>
	/// <param name="sender">The in-memory transport sender.</param>
	/// <param name="expectedCount">The expected number of sent messages.</param>
	public static void ShouldHaveSent(this InMemoryTransportSender sender, int expectedCount)
	{
		ArgumentNullException.ThrowIfNull(sender);
		sender.SentMessages.Count.ShouldBe(expectedCount, "Unexpected sent message count.");
	}

	/// <summary>
	/// Asserts that the sender is configured for the specified destination.
	/// </summary>
	/// <param name="sender">The in-memory transport sender.</param>
	/// <param name="expectedDestination">The expected destination name.</param>
	public static void ShouldHaveSentTo(this InMemoryTransportSender sender, string expectedDestination)
	{
		ArgumentNullException.ThrowIfNull(sender);
		sender.Destination.ShouldBe(expectedDestination, "Unexpected sender destination.");
	}

	/// <summary>
	/// Asserts that at least one sent message matches the specified predicate.
	/// </summary>
	/// <param name="sender">The in-memory transport sender.</param>
	/// <param name="predicate">The predicate to match against sent messages.</param>
	public static void ShouldHaveSentMessageMatching(this InMemoryTransportSender sender, Func<TransportMessage, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(sender);
		ArgumentNullException.ThrowIfNull(predicate);
		sender.SentMessages.ShouldContain(
			m => predicate(m),
			"No sent message matched the specified predicate.");
	}

	#endregion Transport Sender Assertions

	#region Transport Receiver Assertions

	/// <summary>
	/// Asserts that exactly <paramref name="expectedCount"/> messages were acknowledged by the receiver.
	/// </summary>
	/// <param name="receiver">The in-memory transport receiver.</param>
	/// <param name="expectedCount">The expected number of acknowledged messages.</param>
	public static void ShouldHaveAcknowledged(this InMemoryTransportReceiver receiver, int expectedCount)
	{
		ArgumentNullException.ThrowIfNull(receiver);
		receiver.AcknowledgedMessages.Count.ShouldBe(expectedCount, "Unexpected acknowledged message count.");
	}

	/// <summary>
	/// Asserts that exactly <paramref name="expectedCount"/> messages were rejected by the receiver.
	/// </summary>
	/// <param name="receiver">The in-memory transport receiver.</param>
	/// <param name="expectedCount">The expected number of rejected messages.</param>
	public static void ShouldHaveRejected(this InMemoryTransportReceiver receiver, int expectedCount)
	{
		ArgumentNullException.ThrowIfNull(receiver);
		receiver.RejectedMessages.Count.ShouldBe(expectedCount, "Unexpected rejected message count.");
	}

	#endregion Transport Receiver Assertions

	#region IMessageResult Assertions

	/// <summary>
	/// Asserts that the message result indicates successful completion.
	/// </summary>
	/// <param name="result">The message result.</param>
	public static void ShouldHaveCompleted(this IMessageResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		result.IsSuccess.ShouldBeTrue("Expected message result to indicate success, but it was a failure.");
	}

	/// <summary>
	/// Asserts that the message result indicates a failure.
	/// </summary>
	/// <param name="result">The message result.</param>
	public static void ShouldHaveFailed(this IMessageResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		result.IsSuccess.ShouldBeFalse("Expected message result to indicate failure, but it was successful.");
	}

	/// <summary>
	/// Asserts that the message result indicates a failure with a non-null error message.
	/// </summary>
	/// <param name="result">The message result.</param>
	/// <param name="expectedSubstring">Optional substring expected in the error message.</param>
	public static void ShouldHaveFailedWithError(this IMessageResult result, string? expectedSubstring = null)
	{
		ArgumentNullException.ThrowIfNull(result);
		result.IsSuccess.ShouldBeFalse("Expected message result to indicate failure, but it was successful.");
		result.ErrorMessage.ShouldNotBeNullOrWhiteSpace("Expected failure to have an error message.");

		if (expectedSubstring is not null)
		{
			result.ErrorMessage.ShouldContain(expectedSubstring);
		}
	}

	#endregion IMessageResult Assertions

	#region DispatchTestHarness Assertions

	/// <summary>
	/// Asserts that the harness's dispatched message log contains at least one message
	/// of the specified type, providing a shorthand for harness.Dispatched.ShouldHaveDispatched.
	/// </summary>
	/// <typeparam name="TMessage">The expected message type.</typeparam>
	/// <param name="harness">The dispatch test harness.</param>
	public static void ShouldHavePublished<TMessage>(this DispatchTestHarness harness)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(harness);
		harness.Dispatched.ShouldHaveDispatched<TMessage>();
	}

	/// <summary>
	/// Asserts that the harness's dispatched message log contains exactly
	/// <paramref name="expectedCount"/> messages of the specified type.
	/// </summary>
	/// <typeparam name="TMessage">The expected message type.</typeparam>
	/// <param name="harness">The dispatch test harness.</param>
	/// <param name="expectedCount">The expected number of published messages.</param>
	public static void ShouldHavePublished<TMessage>(this DispatchTestHarness harness, int expectedCount)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(harness);
		harness.Dispatched.ShouldHaveDispatched<TMessage>(expectedCount);
	}

	#endregion DispatchTestHarness Assertions

	#region Saga Assertions

	/// <summary>
	/// Asserts that the saga is in the expected completed state.
	/// </summary>
	/// <param name="saga">The saga instance.</param>
	/// <param name="expectedCompleted"><see langword="true"/> if the saga should be completed;
	/// <see langword="false"/> if it should still be active.</param>
	public static void SagaShouldBeInState(this ISaga saga, bool expectedCompleted)
	{
		ArgumentNullException.ThrowIfNull(saga);
		saga.IsCompleted.ShouldBe(expectedCompleted,
			expectedCompleted
				? "Expected saga to be completed, but it is still active."
				: "Expected saga to be active, but it is completed.");
	}

	/// <summary>
	/// Asserts that the saga has completed.
	/// </summary>
	/// <param name="saga">The saga instance.</param>
	public static void SagaShouldBeCompleted(this ISaga saga)
	{
		ArgumentNullException.ThrowIfNull(saga);
		saga.IsCompleted.ShouldBeTrue("Expected saga to be completed, but it is still active.");
	}

	/// <summary>
	/// Asserts that the saga is still active (not completed).
	/// </summary>
	/// <param name="saga">The saga instance.</param>
	public static void SagaShouldBeActive(this ISaga saga)
	{
		ArgumentNullException.ThrowIfNull(saga);
		saga.IsCompleted.ShouldBeFalse("Expected saga to be active, but it is completed.");
	}

	/// <summary>
	/// Asserts that the typed saga state matches the expected predicate.
	/// </summary>
	/// <typeparam name="TSagaState">The saga state type.</typeparam>
	/// <param name="saga">The saga instance.</param>
	/// <param name="statePredicate">Predicate to validate the saga state.</param>
	public static void SagaShouldHaveState<TSagaState>(
		this ISaga<TSagaState> saga,
		Func<TSagaState, bool> statePredicate)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(saga);
		ArgumentNullException.ThrowIfNull(statePredicate);
		statePredicate(saga.State).ShouldBeTrue(
			$"Saga state of type {typeof(TSagaState).Name} did not match the expected predicate.");
	}

	#endregion Saga Assertions
}
