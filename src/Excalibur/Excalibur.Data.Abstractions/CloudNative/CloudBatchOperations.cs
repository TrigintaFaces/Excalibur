// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CloudNative;

/// <summary>
/// A batch operation that carries the document payload the operation writes.
/// </summary>
/// <remarks>
/// <see cref="ICloudBatchOperation"/> names <em>what</em> to do and to <em>which</em> document id, but not the
/// content to write. Every write operation other than a delete must also supply that content, and it does so by
/// implementing this interface. Providers resolve the payload through this contract, so an operation that declares
/// a writing <see cref="ICloudBatchOperation.OperationType"/> without implementing it is rejected rather than
/// skipped.
/// </remarks>
public interface ICloudBatchDocumentOperation : ICloudBatchOperation
{
	/// <summary>
	/// Gets the document the operation writes.
	/// </summary>
	object Document { get; }
}

/// <summary>
/// Batch operation that creates a document, failing if one already exists at the id.
/// </summary>
/// <param name="documentId"> The document id to create. </param>
/// <param name="document"> The document to write. </param>
public sealed class CloudBatchCreateOperation(string documentId, object document) : ICloudBatchDocumentOperation
{
	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Create;

	/// <inheritdoc/>
	public string DocumentId { get; } = documentId ?? throw new ArgumentNullException(nameof(documentId));

	/// <inheritdoc/>
	public object Document { get; } = document ?? throw new ArgumentNullException(nameof(document));
}

/// <summary>
/// Batch operation that replaces an existing document.
/// </summary>
/// <param name="documentId"> The document id to replace. </param>
/// <param name="document"> The replacement document. </param>
public sealed class CloudBatchReplaceOperation(string documentId, object document) : ICloudBatchDocumentOperation
{
	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Replace;

	/// <inheritdoc/>
	public string DocumentId { get; } = documentId ?? throw new ArgumentNullException(nameof(documentId));

	/// <inheritdoc/>
	public object Document { get; } = document ?? throw new ArgumentNullException(nameof(document));
}

/// <summary>
/// Batch operation that writes a document whether or not one already exists at the id.
/// </summary>
/// <param name="documentId"> The document id to write. </param>
/// <param name="document"> The document to write. </param>
public sealed class CloudBatchUpsertOperation(string documentId, object document) : ICloudBatchDocumentOperation
{
	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Upsert;

	/// <inheritdoc/>
	public string DocumentId { get; } = documentId ?? throw new ArgumentNullException(nameof(documentId));

	/// <inheritdoc/>
	public object Document { get; } = document ?? throw new ArgumentNullException(nameof(document));
}

/// <summary>
/// Batch operation that deletes a document.
/// </summary>
/// <param name="documentId"> The document id to delete. </param>
public sealed class CloudBatchDeleteOperation(string documentId) : ICloudBatchOperation
{
	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Delete;

	/// <inheritdoc/>
	public string DocumentId { get; } = documentId ?? throw new ArgumentNullException(nameof(documentId));
}
