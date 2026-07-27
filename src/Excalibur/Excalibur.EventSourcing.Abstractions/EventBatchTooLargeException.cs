// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing;

/// <summary>
/// The exception thrown when a single atomic append exceeds the maximum number of events a provider can
/// write in one all-or-nothing operation.
/// </summary>
/// <remarks>
/// <para>
/// Some event stores impose a hard cap on the number of events in a single atomic append (for example,
/// Amazon DynamoDB's <c>TransactWriteItems</c> caps at 100 items and offers no larger atomic primitive).
/// Splitting the append across multiple calls would risk a torn event-stream prefix, which event sourcing
/// must never produce, so a batch above the cap is rejected up front rather than partially written.
/// </para>
/// <para>
/// This is a caller/usage error — the client asked for something the provider cannot do atomically — so it
/// derives from <see cref="ArgumentOutOfRangeException"/> and is surfaced as a thrown exception rather than
/// a returned <see cref="AppendResult"/> failure. Callers split the append into batches of at most
/// <see cref="MaxBatchSize"/> events, or opt into a provider's non-atomic path where one is offered.
/// </para>
/// </remarks>
public sealed class EventBatchTooLargeException : ArgumentOutOfRangeException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EventBatchTooLargeException"/> class.
	/// </summary>
	/// <param name="paramName">The name of the parameter that carried the oversized batch.</param>
	/// <param name="actualCount">The number of events the caller attempted to append atomically.</param>
	/// <param name="maxBatchSize">The maximum number of events the provider can append in one atomic operation.</param>
	/// <param name="message">A message describing the limit and how to work within it.</param>
	public EventBatchTooLargeException(string? paramName, int actualCount, int maxBatchSize, string message)
		: base(paramName, actualCount, message)
	{
		ActualCount = actualCount;
		MaxBatchSize = maxBatchSize;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventBatchTooLargeException"/> class.
	/// </summary>
	public EventBatchTooLargeException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventBatchTooLargeException"/> class with a message.
	/// </summary>
	/// <param name="message">A message describing the limit and how to work within it.</param>
	public EventBatchTooLargeException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EventBatchTooLargeException"/> class with a message and
	/// an inner exception.
	/// </summary>
	/// <param name="message">A message describing the limit and how to work within it.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public EventBatchTooLargeException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the number of events the caller attempted to append in a single atomic operation.
	/// </summary>
	/// <value>The attempted event count, or <c>0</c> when not specified.</value>
	public int ActualCount { get; }

	/// <summary>
	/// Gets the maximum number of events the provider can append in one atomic operation.
	/// </summary>
	/// <value>The provider's per-append atomic limit, or <c>0</c> when not specified.</value>
	public int MaxBatchSize { get; }
}
