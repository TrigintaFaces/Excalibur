// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;
using StreamsAttributeValue = Amazon.DynamoDBStreams.Model.AttributeValue;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Converts between DynamoDB Streams attribute values and DynamoDB v2 attribute values.
/// </summary>
/// <remarks>
/// Empty maps and lists remain maps and lists when the Streams SDK leaves unset collections null
/// (the default <c>AWSConfigs.InitializeCollections = false</c>). Legacy collection initialization
/// makes empty map and empty list values indistinguishable. Identifiable scalar values and nonempty
/// collections still convert; an otherwise untyped value containing both empty containers is refused.
/// The converter never changes the process-wide SDK configuration.
/// </remarks>
public static class DynamoDbAttributeValueConverter
{
	/// <summary>
	/// Converts a DynamoDB Streams attribute value map to a DynamoDB v2 attribute value map.
	/// </summary>
	/// <param name="streamValues">The streams attribute value map to convert.</param>
	/// <returns>The converted attribute value map, or <see langword="null"/> if the input is null.</returns>
	/// <exception cref="InvalidOperationException">
	/// A value has indistinguishable empty map and list representations, as can occur with legacy
	/// automatic collection initialization. Keep the SDK v4 default collection behavior to preserve its type.
	/// </exception>
	public static Dictionary<string, DynamoDbAttributeValue>? ToAttributeValueMap(
			Dictionary<string, StreamsAttributeValue>? streamValues)
	{
		if (streamValues is null)
		{
			return null;
		}

		if (streamValues.Count == 0)
		{
			return new Dictionary<string, DynamoDbAttributeValue>(StringComparer.Ordinal);
		}

		var converted = new Dictionary<string, DynamoDbAttributeValue>(
				streamValues.Count,
				StringComparer.Ordinal);

		foreach (var entry in streamValues)
		{
			converted[entry.Key] = ToAttributeValue(entry.Value);
		}

		return converted;
	}

	private static DynamoDbAttributeValue ToAttributeValue(
			StreamsAttributeValue streamValue)
	{
		ArgumentNullException.ThrowIfNull(streamValue);

		var converted = new DynamoDbAttributeValue
		{
			S = streamValue.S,
			N = streamValue.N,
			B = streamValue.B,
			BOOL = streamValue.BOOL,
			NULL = streamValue.NULL,
		};

		// Streams values have no IsMSet/IsLSet flags. In legacy initialization mode an
		// untouched collection can be an empty instance, so only copy sets with members.
		if (streamValue.SS is { Count: > 0 }) { converted.SS = streamValue.SS; }
		if (streamValue.NS is { Count: > 0 }) { converted.NS = streamValue.NS; }
		if (streamValue.BS is { Count: > 0 }) { converted.BS = streamValue.BS; }

		var map = streamValue.M;
		var list = streamValue.L;
		var identified = streamValue.S is not null || streamValue.N is not null || streamValue.B is not null
			|| streamValue.BOOL.HasValue || streamValue.NULL.HasValue
			|| streamValue.SS is { Count: > 0 } || streamValue.NS is { Count: > 0 } || streamValue.BS is { Count: > 0 }
			|| map is { Count: > 0 } || list is { Count: > 0 };
		if (!identified && map is not null && list is not null)
		{
			throw new InvalidOperationException(
				"The DynamoDB Streams value contains both an empty map and an empty list; its type is ambiguous. "
				+ "Use the default AWSConfigs.InitializeCollections = false so unset collections remain null.");
		}

		if (map is { Count: > 0 } || (!identified && map is not null))
		{
			converted.M = ToAttributeValueMap(map);
		}

		if (list is { Count: > 0 } || (!identified && list is not null))
		{
			converted.L = [.. list!.Select(ToAttributeValue)];
		}

		return converted;
	}
}
