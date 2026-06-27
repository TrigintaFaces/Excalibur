// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Focused value type grouping the supplemental identity and versioning metadata for a message.
/// </summary>
/// <remarks>
/// Composed onto <see cref="MessageMetadata"/> alongside the core dispatch identity fields
/// (<see cref="MessageMetadata.MessageId"/>, <see cref="MessageMetadata.CorrelationId"/>,
/// <see cref="MessageMetadata.CausationId"/>) which remain on the root for the
/// <see cref="IMessageMetadata"/> contract. This group carries the optional external identifier
/// and the message format/contract versioning fields. Holds at most ten properties to satisfy the
/// Microsoft-first focused-value-type design guideline.
/// </remarks>
public readonly record struct MessageIdentity
{
	/// <summary>
	/// Gets an external system identifier for the message.
	/// </summary>
	/// <value> The external identifier or <see langword="null"/>. </value>
	public string? ExternalId { get; init; }

	/// <summary>
	/// Gets the encoding used for the message content.
	/// </summary>
	/// <value> The content encoding or <see langword="null"/>. </value>
	public string? ContentEncoding { get; init; }

	/// <summary>
	/// Gets the version of the message format.
	/// </summary>
	/// <value> The message format version. </value>
	public string? MessageVersion { get; init; }

	/// <summary>
	/// Gets the version of the serializer used for the message.
	/// </summary>
	/// <value> The serializer version. </value>
	public string? SerializerVersion { get; init; }

	/// <summary>
	/// Gets the version of the message contract.
	/// </summary>
	/// <value> The contract version. </value>
	public string? ContractVersion { get; init; }
}
