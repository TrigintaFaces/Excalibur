// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing versioning metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
public static class MetadataVersioningExtensions
{
	/// <summary>
	/// Gets the encoding of the message content.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The content encoding (e.g., "utf-8"), or null if not set. </returns>
	public static string? GetContentEncoding(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ContentEncoding, out var value) ? value as string : null;

	/// <summary>
	/// Gets the schema version of the message payload structure.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The message version, or "1.0" if not set. </returns>
	public static string GetMessageVersion(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.MessageVersion, out var value) && value is string v ? v : "1.0";

	/// <summary>
	/// Gets the version of the serializer used to encode the message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The serializer version, or "1.0" if not set. </returns>
	public static string GetSerializerVersion(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.SerializerVersion, out var value) && value is string v ? v : "1.0";

	/// <summary>
	/// Gets the overall API contract version this message adheres to.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The contract version, or "1.0.0" if not set. </returns>
	public static string GetContractVersion(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ContractVersion, out var value) && value is string v ? v : "1.0.0";
}
