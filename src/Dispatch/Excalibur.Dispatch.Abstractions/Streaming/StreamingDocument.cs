// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Streaming;

/// <summary>
/// Abstract base record for document-style messages that support streaming scenarios.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StreamingDocument"/> provides a reference-type base for documents that may be
/// streamed as part of an <see cref="IAsyncEnumerable{T}"/> sequence. Unlike <see cref="Chunk{T}"/>
/// which wraps arbitrary data with positional metadata, this type represents documents that
/// inherently participate in streaming workflows.
/// </para>
/// <para>
/// Use cases include:
/// </para>
/// <list type="bullet">
/// <item>Batch import/export operations where each document represents a record</item>
/// <item>Event replay scenarios with per-document metadata</item>
/// <item>Pipeline processing where documents flow through multiple handlers</item>
/// <item>Change data capture streams where each change is a document</item>
/// </list>
/// <para>
/// Derived types should add domain-specific properties while inheriting the streaming metadata.
/// </para>
/// </remarks>
/// <param name="StreamId">
/// A unique identifier for the stream this document belongs to. Used to correlate
/// documents within the same logical sequence.
/// </param>
/// <param name="SequenceNumber">
/// The zero-based position of this document within its stream. Combined with
/// <paramref name="StreamId"/>, provides total ordering guarantees.
/// </param>
public abstract record StreamingDocument(
	string StreamId,
	long SequenceNumber) : IDispatchDocument
{
	/// <summary>
	/// Gets the correlation identifier for tracing this document across services.
	/// </summary>
	/// <value>
	/// A correlation ID for distributed tracing, or <see langword="null"/> if not set.
	/// </value>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets the timestamp when this document was created or received.
	/// </summary>
	/// <value>
	/// The UTC timestamp of document creation.
	/// </value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets a value indicating whether this document marks the end of the stream.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if this is the terminal document in the stream;
	/// otherwise, <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Handlers can use this property to perform finalization logic when the stream completes.
	/// </remarks>
	public bool IsEndOfStream { get; init; }
}
