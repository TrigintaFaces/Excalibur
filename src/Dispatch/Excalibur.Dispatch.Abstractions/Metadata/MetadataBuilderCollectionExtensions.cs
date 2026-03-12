// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for adding collection-based metadata to <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderCollectionExtensions
{
	/// <summary>
	/// Adds multiple headers to the message metadata.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="headers"> The headers to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddHeaders(this IMessageMetadataBuilder builder, IEnumerable<KeyValuePair<string, string>> headers)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(headers);

		foreach (var header in headers)
		{
			builder.AddHeader(header.Key, header.Value);
		}

		return builder;
	}

	/// <summary>
	/// Sets the attributes dictionary, replacing any existing attributes.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="attributes"> The attributes to set. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddAttributes(this IMessageMetadataBuilder builder, IEnumerable<KeyValuePair<string, object>> attributes)
		=> builder.WithProperty(MetadataPropertyKeys.Attributes, attributes);

	/// <summary>
	/// Adds a single attribute to the message metadata.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="key"> The attribute key. </param>
	/// <param name="value"> The attribute value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddAttribute(this IMessageMetadataBuilder builder, string key, object value)
		=> builder.AddAttributes(new[] { new KeyValuePair<string, object>(key, value) });

	/// <summary>
	/// Adds multiple custom properties to the message metadata.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="properties"> The properties to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddProperties(this IMessageMetadataBuilder builder, IEnumerable<KeyValuePair<string, object>> properties)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(properties);

		foreach (var property in properties)
		{
			builder.WithProperty(property.Key, property.Value);
		}

		return builder;
	}

	/// <summary>
	/// Adds a single custom property to the message metadata.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddProperty(this IMessageMetadataBuilder builder, string key, object value)
		=> builder.WithProperty(key, value);

	/// <summary>
	/// Sets the items dictionary, replacing any existing items.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="items"> The items to set. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddItems(this IMessageMetadataBuilder builder, IEnumerable<KeyValuePair<string, object>> items)
		=> builder.WithProperty(MetadataPropertyKeys.Items, items);

	/// <summary>
	/// Adds a single item to the message metadata.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="key"> The item key. </param>
	/// <param name="value"> The item value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder AddItem(this IMessageMetadataBuilder builder, string key, object value)
		=> builder.AddItems(new[] { new KeyValuePair<string, object>(key, value) });
}
