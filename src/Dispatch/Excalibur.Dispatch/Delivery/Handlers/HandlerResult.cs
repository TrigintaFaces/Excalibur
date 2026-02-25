// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Result wrapper that avoids boxing for value types.
/// </summary>
/// <typeparam name="TResult"> The type of the result. </typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct HandlerResult<TResult> : IEquatable<HandlerResult<TResult>>
{
	private readonly TResult? _value;

	/// <summary>
	/// Initializes a new instance of the <see cref="HandlerResult{TResult}"/> struct.
	/// Creates a successful result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public HandlerResult(TResult value)
	{
		_value = value;
		HasValue = true;
		Exception = null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HandlerResult{TResult}"/> struct.
	/// Creates a failed result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public HandlerResult(Exception exception)
	{
		_value = default;
		HasValue = false;
		Exception = exception ?? throw new ArgumentNullException(nameof(exception));
	}

	/// <summary>
	/// Gets a value indicating whether the result has a value.
	/// </summary>
	/// <value>The current <see cref="HasValue"/> value.</value>
	public bool HasValue { get; }

	/// <summary>
	/// Gets a value indicating whether the result is faulted.
	/// </summary>
	/// <value>The current <see cref="IsFaulted"/> value.</value>
	public bool IsFaulted => Exception != null;

	/// <summary>
	/// Gets the value if successful.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	/// <value>The value if successful.</value>
	public TResult Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (Exception != null)
			{
				throw new InvalidOperationException(ErrorMessages.CannotAccessValueOfFaultedResult, Exception);
			}

			if (!HasValue)
			{
				throw new InvalidOperationException(ErrorMessages.ResultHasNoValue);
			}

			return _value!;
		}
	}

	/// <summary>
	/// Gets the exception if faulted.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; }

	/// <summary>
	/// Creates a result from a value.
	/// </summary>
	/// <param name="value"> The value to convert. </param>
	/// <returns> A HandlerResult containing the value. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static HandlerResult<TResult> FromTResult(TResult value) => new(value);

	/// <summary>
	/// Creates a result from an exception.
	/// </summary>
	/// <param name="exception"> The exception to convert. </param>
	/// <returns> A HandlerResult containing the exception. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static HandlerResult<TResult> FromException(Exception exception) => new(exception);

	/// <summary>
	/// Implicitly converts a value to a result.
	/// </summary>
	/// <param name="value"></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator HandlerResult<TResult>(TResult value) => new(value);

	/// <summary>
	/// Implicitly converts an exception to a result.
	/// </summary>
	/// <param name="exception"></param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator HandlerResult<TResult>(Exception exception) => new(exception);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool Equals(HandlerResult<TResult> other)
	{
		if (HasValue != other.HasValue)
		{
			return false;
		}

		if (Exception != other.Exception)
		{
			return false;
		}

		if (HasValue)
		{
			return EqualityComparer<TResult>.Default.Equals(_value, other._value);
		}

		return true;
	}

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"></param>
	/// <returns></returns>
	public override bool Equals(object? obj) => obj is HandlerResult<TResult> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns></returns>
	public override int GetHashCode()
	{
		var hash = default(HashCode);
		hash.Add(HasValue);
		hash.Add(Exception);
		if (HasValue && _value is not null)
		{
			hash.Add(_value);
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	public static bool operator ==(HandlerResult<TResult> left, HandlerResult<TResult> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	public static bool operator !=(HandlerResult<TResult> left, HandlerResult<TResult> right) => !left.Equals(right);
}
