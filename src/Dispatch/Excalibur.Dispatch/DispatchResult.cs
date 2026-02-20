// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Default implementation of dispatch result.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DispatchResult" /> class. </remarks>
/// <param name="isSuccess"> Whether the dispatch was successful. </param>
/// <param name="result"> The result data. </param>
/// <param name="exception"> Any exception that occurred. </param>
/// <param name="metadata"> Additional metadata. </param>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class DispatchResult(
	bool isSuccess,
	object? result = null,
	Exception? exception = null,
	IDictionary<string, object>? metadata = null) : IDispatchResult
{
	/// <summary>
	/// Gets a value indicating whether the dispatch operation was successful.
	/// </summary>
	/// <value>The current <see cref="IsSuccess"/> value.</value>
	public bool IsSuccess { get; } = isSuccess;

	/// <summary>
	/// Gets the result data from the dispatch operation.
	/// </summary>
	/// <value>The current <see cref="Result"/> value.</value>
	public object? Result { get; } = result;

	/// <summary>
	/// Gets any exception that occurred during dispatch.
	/// </summary>
	/// <value>The current <see cref="Exception"/> value.</value>
	public Exception? Exception { get; } = exception;

	/// <summary>
	/// Gets additional metadata about the dispatch operation.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public IDictionary<string, object>? Metadata { get; } = metadata;

	/// <summary>
	/// Creates a successful dispatch result.
	/// </summary>
	/// <param name="result"> The result data. </param>
	/// <param name="metadata"> Additional metadata. </param>
	/// <returns> A successful dispatch result. </returns>
	public static DispatchResult Success(object? result = null, IDictionary<string, object>? metadata = null)
		=> new(isSuccess: true, result, exception: null, metadata);

	/// <summary>
	/// Creates a failed dispatch result.
	/// </summary>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="metadata"> Additional metadata. </param>
	/// <returns> A failed dispatch result. </returns>
	public static DispatchResult Failure(Exception exception, IDictionary<string, object>? metadata = null)
		=> new(isSuccess: false, result: null, exception, metadata);
}
