// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Represents a signed message with its content and signature.
/// </summary>
public sealed class SignedMessage
{
	/// <summary>
	/// Gets or sets the message content.
	/// </summary>
	/// <value>
	/// The message content as a string.
	/// </value>
	public required string Content { get; set; }

	/// <summary>
	/// Gets or sets the signature.
	/// </summary>
	/// <value>
	/// The cryptographic signature as a string.
	/// </value>
	public required string Signature { get; set; }

	/// <summary>
	/// Gets or sets the algorithm used for signing.
	/// </summary>
	/// <value>
	/// The <see cref="SigningAlgorithm"/> used to create the signature.
	/// </value>
	public SigningAlgorithm Algorithm { get; set; }

	/// <summary>
	/// Gets or sets the key identifier used for signing.
	/// </summary>
	/// <value>
	/// The key identifier, or <see langword="null"/> if no key identifier is specified.
	/// </value>
	public string? KeyId { get; set; }

	/// <summary>
	/// Gets or sets when the message was signed.
	/// </summary>
	/// <value>
	/// The timestamp when the message was signed.
	/// </value>
	public DateTimeOffset SignedAt { get; set; }

	/// <summary>
	/// Gets or initializes additional metadata.
	/// </summary>
	/// <value>
	/// A dictionary of additional metadata as key-value pairs, or an empty dictionary if no metadata is provided.
	/// </value>
	public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
