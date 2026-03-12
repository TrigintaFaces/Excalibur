// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing collection metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
/// <remarks>
/// The Attributes and Items dictionaries were previously on the <see cref="IMessageMetadata"/> interface
/// alongside Properties and Headers. They are now stored inside the Properties dictionary itself.
/// </remarks>
public static class MetadataCollectionExtensions
{
	/// <summary>
	/// Gets the collection of message attributes.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The attributes dictionary, or an empty dictionary if not set. </returns>
	public static IReadOnlyDictionary<string, object> GetAttributes(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Attributes, out var value) && value is IReadOnlyDictionary<string, object> attrs
			? attrs
			: new Dictionary<string, object>(0, StringComparer.Ordinal);

	/// <summary>
	/// Gets the collection of items for pipeline processing.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The items dictionary, or an empty dictionary if not set. </returns>
	public static IReadOnlyDictionary<string, object> GetItems(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Items, out var value) && value is IReadOnlyDictionary<string, object> items
			? items
			: new Dictionary<string, object>(0, StringComparer.Ordinal);
}
