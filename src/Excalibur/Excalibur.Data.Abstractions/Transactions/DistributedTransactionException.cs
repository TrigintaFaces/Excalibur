// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Transactions;

/// <summary>
/// Exception thrown when a distributed transaction operation fails.
/// </summary>
public class DistributedTransactionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedTransactionException"/> class.
	/// </summary>
	public DistributedTransactionException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedTransactionException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public DistributedTransactionException(string message) : base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedTransactionException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public DistributedTransactionException(string message, Exception innerException) : base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the transaction identifier associated with this failure.
	/// </summary>
	/// <value>The transaction identifier, or <see langword="null"/> if not available.</value>
	public string? TransactionId { get; init; }

	/// <summary>
	/// Gets the participant identifiers that failed during the transaction.
	/// </summary>
	/// <value>The collection of failed participant identifiers.</value>
	public IReadOnlyList<string> FailedParticipantIds { get; init; } = [];
}
