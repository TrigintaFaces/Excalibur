// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.ZeroAlloc;

/// <summary>
/// Zero-allocation struct-based implementation of IMessageResult for high-performance scenarios.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct StructMessageResult : IMessageResult, IEquatable<StructMessageResult>
{
	private readonly ResultFlags _flags;

	/// <summary>
	/// Initializes a new instance of the <see cref="StructMessageResult" /> struct.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public StructMessageResult(
		bool succeeded = true,
		IMessageProblemDetails? problemDetails = null,
		bool cacheHit = false)
	{
		_flags = ResultFlags.None;

		if (succeeded)
		{
			_flags |= ResultFlags.Succeeded;
		}

		if (cacheHit)
		{
			_flags |= ResultFlags.CacheHit;
		}

		ProblemDetails = problemDetails;
	}

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	/// <value>
	/// A value indicating whether the operation succeeded.
	/// </value>
	public bool Succeeded => (_flags & ResultFlags.Succeeded) != 0;

	/// <summary>
	/// Gets the problem details if the operation failed.
	/// </summary>
	/// <value>The current <see cref="ProblemDetails"/> value.</value>
	public IMessageProblemDetails? ProblemDetails { get; }

	/// <summary>
	/// Gets the routing decision.
	/// </summary>
	/// <value>The current <see cref="RoutingDecision"/> value.</value>
	public static RoutingDecision? RoutingDecision => DefaultRoutingDecision;

	/// <summary>
	/// Gets the validation result.
	/// </summary>
	/// <value>The current <see cref="ValidationResult"/> value.</value>
	public static IValidationResult? ValidationResult => DefaultValidationResult.Instance;

	/// <summary>
	/// Gets the authorization result.
	/// </summary>
	/// <value>The current <see cref="AuthorizationResult"/> value.</value>
	public static IAuthorizationResult? AuthorizationResult => DefaultAuthorizationResult.Instance;

	/// <summary>
	/// Gets a value indicating whether this was a cache hit.
	/// </summary>
	/// <value>
	/// A value indicating whether this was a cache hit.
	/// </value>
	public bool CacheHit => (_flags & ResultFlags.CacheHit) != 0;

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage => ProblemDetails?.Detail;

	/// <summary>
	/// Creates a successful result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult Success() => new(succeeded: true);

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult Failed(IMessageProblemDetails problemDetails) =>
		new(succeeded: false, problemDetails: problemDetails);

	/// <summary>
	/// Creates a cache hit result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult FromCache() => new(succeeded: true, cacheHit: true);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(StructMessageResult other) => _flags == other._flags && ReferenceEquals(ProblemDetails, other.ProblemDetails);

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is StructMessageResult other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(_flags, ProblemDetails);

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(StructMessageResult left, StructMessageResult right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(StructMessageResult left, StructMessageResult right) => !left.Equals(right);

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => AuthorizationResult;

	/// <summary>
	/// Flags enum for compact storage.
	/// </summary>
	[Flags]
	private enum ResultFlags : byte
	{
		None = 0,
		Succeeded = 1 << 0,
		CacheHit = 1 << 1,
	}

	/// <summary>
	/// Singleton default routing decision to avoid allocations.
	/// </summary>
	private static readonly RoutingDecision DefaultRoutingDecision =
		RoutingDecision.Success("local", []);

	/// <summary>
	/// Singleton default validation result to avoid allocations.
	/// </summary>
	private sealed class DefaultValidationResult : IValidationResult
	{
		public static readonly IValidationResult Instance = new DefaultValidationResult();

		private DefaultValidationResult()
		{
		}

		public bool IsValid { get; set; } = true;

		public IReadOnlyCollection<object> Errors => [];

		public static IValidationResult Failed(params object[] errors) =>
						throw new NotSupportedException(ErrorMessages.UseStructValidationResultForFailedResults);

		public static IValidationResult Success() => Instance;
	}

	/// <summary>
	/// Singleton default authorization result to avoid allocations.
	/// </summary>
	private sealed class DefaultAuthorizationResult : IAuthorizationResult
	{
		public static readonly IAuthorizationResult Instance = new DefaultAuthorizationResult();

		private DefaultAuthorizationResult()
		{
		}

		public bool IsAuthorized => true;

		public string? FailureMessage => null;
	}
}

/// <summary>
/// Zero-allocation struct-based implementation of IMessageResult{T} for high-performance scenarios.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="StructMessageResult{T}" /> struct. </remarks>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct StructMessageResult<T>(
	T? returnValue,
	bool succeeded = true,
	IMessageProblemDetails? problemDetails = null,
	bool cacheHit = false) : IMessageResult<T>, IEquatable<StructMessageResult<T>>
{
	private readonly StructMessageResult _baseResult = new(succeeded, problemDetails, cacheHit);

	/// <summary>
	/// Gets the return value.
	/// </summary>
	/// <value>The current <see cref="ReturnValue"/> value.</value>
	public T? ReturnValue { get; } = returnValue;

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	/// <value>The current <see cref="Succeeded"/> value.</value>
	public bool Succeeded => _baseResult.Succeeded;

	/// <summary>
	/// Gets the problem details if the operation failed.
	/// </summary>
	/// <value>The current <see cref="ProblemDetails"/> value.</value>
	public IMessageProblemDetails? ProblemDetails => _baseResult.ProblemDetails;

	// R0.8: Make property static - these properties implement IMessageResult interface which requires instance members
#pragma warning disable MA0041

	/// <summary>
	/// Gets the routing decision.
	/// </summary>
	/// <value>The current <see cref="RoutingDecision"/> value.</value>
	public RoutingDecision? RoutingDecision => StructMessageResult.RoutingDecision;

	/// <summary>
	/// Gets the validation result.
	/// </summary>
	/// <value>The current <see cref="ValidationResult"/> value.</value>
	public IValidationResult? ValidationResult => StructMessageResult.ValidationResult;

	/// <summary>
	/// Gets the authorization result.
	/// </summary>
	/// <value>The current <see cref="AuthorizationResult"/> value.</value>
	public IAuthorizationResult? AuthorizationResult => StructMessageResult.AuthorizationResult;

#pragma warning restore MA0041

	/// <summary>
	/// Gets a value indicating whether this was a cache hit.
	/// </summary>
	/// <value>The current <see cref="CacheHit"/> value.</value>
	public bool CacheHit => _baseResult.CacheHit;

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage => _baseResult.ErrorMessage;

	/// <summary>
	/// Creates a successful result with a return value.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult<T> Success(T returnValue) =>
		new(returnValue, succeeded: true);

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult<T> Failed(IMessageProblemDetails problemDetails) =>
		new(default, succeeded: false, problemDetails: problemDetails);

	/// <summary>
	/// Creates a cache hit result with a return value.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StructMessageResult<T> FromCache(T returnValue) =>
		new(returnValue, succeeded: true, cacheHit: true);

	/// <summary>
	/// Determines whether the specified result is equal to the current result.
	/// </summary>
	/// <param name="other"> The result to compare with the current result. </param>
	/// <returns> true if the specified result is equal to the current result; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(StructMessageResult<T> other) =>
		_baseResult.Equals(other._baseResult) && EqualityComparer<T>.Default.Equals(ReturnValue, other.ReturnValue);

	/// <summary>
	/// Determines whether the specified object is equal to the current result.
	/// </summary>
	/// <param name="obj"> The object to compare with the current result. </param>
	/// <returns> true if the specified object is equal to the current result; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is StructMessageResult<T> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this result.
	/// </summary>
	/// <returns> A hash code for the current result. </returns>
	public override int GetHashCode() => HashCode.Combine(_baseResult, EqualityComparer<T>.Default.GetHashCode(ReturnValue));

	/// <summary>
	/// Determines whether two results are equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are equal; otherwise, false. </returns>
	public static bool operator ==(StructMessageResult<T> left, StructMessageResult<T> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two results are not equal.
	/// </summary>
	/// <param name="left"> The first result to compare. </param>
	/// <param name="right"> The second result to compare. </param>
	/// <returns> true if the results are not equal; otherwise, false. </returns>
	public static bool operator !=(StructMessageResult<T> left, StructMessageResult<T> right) => !left.Equals(right);

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => AuthorizationResult;
}
