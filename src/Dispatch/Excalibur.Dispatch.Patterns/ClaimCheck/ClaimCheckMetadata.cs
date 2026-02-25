// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Metadata associated with a claim check.
/// </summary>
public sealed class ClaimCheckMetadata
{
	/// <summary>
	/// Gets or sets the message identifier.
	/// </summary>
	/// <value>
	/// The message identifier.
	/// </value>
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	/// <value>
	/// The message type.
	/// </value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type of the payload.
	/// </summary>
	/// <value>
	/// The content type of the payload.
	/// </value>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the encoding used for the payload.
	/// </summary>
	/// <value>
	/// The encoding used for the payload.
	/// </value>
	public string? ContentEncoding { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the payload is compressed.
	/// </summary>
	/// <value>
	/// A value indicating whether the payload is compressed.
	/// </value>
	public bool IsCompressed { get; set; }

	/// <summary>
	/// Gets or sets the original size before compression.
	/// </summary>
	/// <value>
	/// The original size before compression.
	/// </value>
	public long? OriginalSize { get; set; }

	/// <summary>
	/// Gets custom properties.
	/// </summary>
	/// <value>
	/// Custom properties.
	/// </value>
	public Dictionary<string, string> Properties { get; } = [];

	/// <summary>
	/// Gets or sets the correlation identifier for message tracing.
	/// </summary>
	/// <value>
	/// The correlation identifier for message tracing.
	/// </value>
	public string? CorrelationId { get; set; }
}
