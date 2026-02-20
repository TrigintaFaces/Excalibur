// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Exceptions;

/// <summary>
/// Represents a domain-level exception that occurs within the application's business logic.
/// </summary>
/// <remarks>
/// <para>
/// Domain exceptions are pure business-logic errors that should not carry HTTP or API concerns.
/// HTTP status code mapping belongs in the API/middleware layer, not in the domain.
/// </para>
/// </remarks>
[Serializable]
public sealed class DomainException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DomainException" /> class with a default error message.
	/// </summary>
	public DomainException()
			: base(Resources.DomainException_DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DomainException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	public DomainException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DomainException" /> class with a specified error message and an inner exception.
	/// </summary>
	/// <param name="message"> A message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public DomainException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Throws a <see cref="DomainException" /> if the specified condition evaluates to <c> true </c>.
	/// </summary>
	/// <param name="condition"> The condition that determines whether to throw the exception. </param>
	/// <param name="message"> A message describing the exception. </param>
	/// <exception cref="DomainException"> Thrown if <paramref name="condition" /> evaluates to <c> true </c>. </exception>
	public static void ThrowIf(bool condition, string message)
	{
		if (condition)
		{
			throw new DomainException(message);
		}
	}

	/// <summary>
	/// Throws a <see cref="DomainException" /> if the specified condition evaluates to <c> true </c>.
	/// </summary>
	/// <param name="condition"> The condition that determines whether to throw the exception. </param>
	/// <param name="message"> A message describing the exception. </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	/// <exception cref="DomainException"> Thrown if <paramref name="condition" /> evaluates to <c> true </c>. </exception>
	public static void ThrowIf(bool condition, string message, Exception innerException)
	{
		if (condition)
		{
			throw new DomainException(message, innerException);
		}
	}
}
