// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of a message dispatch operation.
/// </summary>
public interface IDispatchResult
{
	/// <summary>
	/// Gets a value indicating whether the dispatch operation was successful.
	/// </summary>
	/// <value> <see langword="true" /> when the dispatch succeeded; otherwise, <see langword="false" />. </value>
	bool IsSuccess { get; }

	/// <summary>
	/// Gets the result data from the dispatch operation.
	/// </summary>
	/// <value> The optional result payload returned by the dispatch. </value>
	object? Result { get; }

	/// <summary>
	/// Gets any exception that occurred during dispatch.
	/// </summary>
	/// <value> The exception thrown during dispatch, if any. </value>
	Exception? Exception { get; }

	/// <summary>
	/// Gets additional metadata about the dispatch operation.
	/// </summary>
	/// <value> The optional metadata dictionary associated with the dispatch. </value>
	IDictionary<string, object>? Metadata { get; }
}
