// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

/// <summary>
/// Represents the result of a typed exception handler's attempt to handle an exception.
/// </summary>
/// <remarks>
/// This is a value type to avoid allocations on the hot path when exceptions are not handled.
/// </remarks>
public readonly struct ExceptionHandlerResult : IEquatable<ExceptionHandlerResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExceptionHandlerResult"/> struct.
	/// </summary>
	/// <param name="isHandled">Whether the exception was handled.</param>
	/// <param name="result">The message result if handled.</param>
	private ExceptionHandlerResult(bool isHandled, IMessageResult? result)
	{
		IsHandled = isHandled;
		Result = result;
	}

	/// <summary>
	/// Gets a value indicating whether the exception was handled.
	/// </summary>
	/// <value><see langword="true"/> if the exception was handled; otherwise, <see langword="false"/>.</value>
	public bool IsHandled { get; }

	/// <summary>
	/// Gets the message result to return when the exception is handled.
	/// </summary>
	/// <value>The message result, or <see langword="null"/> if not handled.</value>
	public IMessageResult? Result { get; }

	/// <summary>
	/// Creates a result indicating the exception was handled with the specified result.
	/// </summary>
	/// <param name="result">The message result to return.</param>
	/// <returns>A handled exception result.</returns>
	public static ExceptionHandlerResult Handled(IMessageResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		return new ExceptionHandlerResult(isHandled: true, result);
	}

	/// <summary>
	/// Creates a result indicating the exception was not handled and should propagate.
	/// </summary>
	/// <returns>An unhandled exception result.</returns>
	public static ExceptionHandlerResult NotHandled() =>
		new(isHandled: false, result: null);

	/// <inheritdoc/>
	public bool Equals(ExceptionHandlerResult other) =>
		IsHandled == other.IsHandled && ReferenceEquals(Result, other.Result);

	/// <inheritdoc/>
	public override bool Equals(object? obj) =>
		obj is ExceptionHandlerResult other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() =>
		HashCode.Combine(IsHandled, Result);

	/// <summary>
	/// Determines whether two <see cref="ExceptionHandlerResult"/> instances are equal.
	/// </summary>
	/// <param name="left">The left operand.</param>
	/// <param name="right">The right operand.</param>
	/// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
	public static bool operator ==(ExceptionHandlerResult left, ExceptionHandlerResult right) =>
		left.Equals(right);

	/// <summary>
	/// Determines whether two <see cref="ExceptionHandlerResult"/> instances are not equal.
	/// </summary>
	/// <param name="left">The left operand.</param>
	/// <param name="right">The right operand.</param>
	/// <returns><see langword="true"/> if the instances are not equal; otherwise, <see langword="false"/>.</returns>
	public static bool operator !=(ExceptionHandlerResult left, ExceptionHandlerResult right) =>
		!left.Equals(right);
}
