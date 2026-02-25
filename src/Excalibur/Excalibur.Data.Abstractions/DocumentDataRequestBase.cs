// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Serves as the base class for implementing document store requests with a specific connection type and return model.
/// </summary>
/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
/// <typeparam name="TResult"> The type of the result returned by the request. </typeparam>
public abstract class DocumentDataRequestBase<TConnection, TResult> : IDocumentDataRequest<TConnection, TResult>
{
	/// <inheritdoc />
	public string CollectionName { get; protected set; } = string.Empty;

	/// <inheritdoc />
	public string OperationType { get; protected set; } = string.Empty;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Parameters { get; protected set; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <inheritdoc />
	public Func<TConnection, Task<TResult>> ResolveAsync { get; protected set; } = null!;

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object>? Options { get; protected set; }

	/// <summary>
	/// Creates a document operation with the specified parameters.
	/// </summary>
	/// <param name="collectionName"> The name of the collection to operate on. </param>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <param name="parameters"> The operation parameters. </param>
	/// <param name="options"> Optional operation-specific options. </param>
	/// <exception cref="ArgumentException"> Thrown when collection name or operation type is null or empty. </exception>
	protected void InitializeOperation(
		string collectionName,
		string operationType,
		IDictionary<string, object> parameters,
		IDictionary<string, object>? options = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
		ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
		ArgumentNullException.ThrowIfNull(parameters);

		CollectionName = collectionName;
		OperationType = operationType;
		Parameters = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
		Options = options is not null ? new Dictionary<string, object>(options, StringComparer.Ordinal) : null;
	}
}
