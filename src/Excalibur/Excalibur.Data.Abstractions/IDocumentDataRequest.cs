// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Represents a document store data request that can be executed against a document database connection.
/// </summary>
/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
/// <typeparam name="TResult"> The type of the result returned by the request. </typeparam>
public interface IDocumentDataRequest<in TConnection, TResult>
{
	/// <summary>
	/// Gets the collection name for the document operation.
	/// </summary>
	/// <value>
	/// The collection name for the document operation.
	/// </value>
	string CollectionName { get; }

	/// <summary>
	/// Gets the operation type (e.g., "Insert", "Find", "Update", "Delete", "Aggregate").
	/// </summary>
	/// <value>
	/// The operation type (e.g., "Insert", "Find", "Update", "Delete", "Aggregate").
	/// </value>
	string OperationType { get; }

	/// <summary>
	/// Gets the operation parameters as a read-only dictionary.
	/// </summary>
	/// <value>
	/// The operation parameters as a read-only dictionary.
	/// </value>
	IReadOnlyDictionary<string, object> Parameters { get; }

	/// <summary>
	/// Gets the function responsible for resolving the request result using the provided connection.
	/// </summary>
	/// <value>
	/// The function responsible for resolving the request result using the provided connection.
	/// </value>
	Func<TConnection, Task<TResult>> ResolveAsync { get; }

	/// <summary>
	/// Gets optional operation-specific options (e.g., write concerns, read preferences).
	/// </summary>
	/// <value>
	/// Optional operation-specific options (e.g., write concerns, read preferences).
	/// </value>
	IReadOnlyDictionary<string, object>? Options { get; }
}
