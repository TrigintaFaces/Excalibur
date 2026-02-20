// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler for processing document-style messages.
/// </summary>
/// <typeparam name="TDocument"> The type of document to handle. </typeparam>
/// <remarks>
/// Implement this interface to process structured documents that flow through the messaging pipeline. Documents typically represent
/// complete business entities or data transfer objects. Common use cases include:
/// <list type="bullet">
/// <item> Processing batch imports or exports </item>
/// <item> Handling complex data transformations </item>
/// <item> Processing reports, invoices, or other business documents </item>
/// <item> ETL (Extract, Transform, Load) operations </item>
/// <item> Handling file uploads or data synchronization </item>
/// </list>
/// Multiple document handlers can process the same document type for different aspects (validation, transformation, storage, notification,
/// etc.). The contravariant in modifier allows handlers to process base document types.
/// </remarks>
public interface IDocumentHandler<in TDocument>
	where TDocument : IDispatchDocument
{
	/// <summary>
	/// Handles the specified document asynchronously.
	/// </summary>
	/// <param name="document"> The document to process. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <remarks>
	/// Implementations should handle large documents efficiently, potentially using streaming or chunking for memory optimization. Consider
	/// implementing progress reporting for long-running document processing operations.
	/// </remarks>
	Task HandleAsync(TDocument document, CancellationToken cancellationToken);
}
