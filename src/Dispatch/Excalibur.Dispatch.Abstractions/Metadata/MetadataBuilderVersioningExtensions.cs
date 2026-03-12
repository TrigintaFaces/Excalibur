// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting versioning properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderVersioningExtensions
{
	/// <summary>
	/// Sets the content encoding of the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="contentEncoding"> The encoding used for the message content. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithContentEncoding(this IMessageMetadataBuilder builder, string? contentEncoding)
		=> builder.WithProperty(MetadataPropertyKeys.ContentEncoding, contentEncoding);

	/// <summary>
	/// Sets the message schema version.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="messageVersion"> The version of the message schema. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithMessageVersion(this IMessageMetadataBuilder builder, string? messageVersion)
		=> builder.WithProperty(MetadataPropertyKeys.MessageVersion, messageVersion);

	/// <summary>
	/// Sets the serializer version used to encode the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="serializerVersion"> The version of the serializer. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithSerializerVersion(this IMessageMetadataBuilder builder, string? serializerVersion)
		=> builder.WithProperty(MetadataPropertyKeys.SerializerVersion, serializerVersion);

	/// <summary>
	/// Sets the contract version for message compatibility.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="contractVersion"> The version of the message contract. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithContractVersion(this IMessageMetadataBuilder builder, string? contractVersion)
		=> builder.WithProperty(MetadataPropertyKeys.ContractVersion, contractVersion);
}
