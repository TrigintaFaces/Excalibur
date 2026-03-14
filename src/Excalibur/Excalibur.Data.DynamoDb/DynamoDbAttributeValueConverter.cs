// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using DynamoDbAttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;
using StreamsAttributeValue = Amazon.DynamoDBStreams.Model.AttributeValue;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Converts between DynamoDB Streams attribute values and DynamoDB v2 attribute values.
/// </summary>
public static class DynamoDbAttributeValueConverter
{
	/// <summary>
	/// Converts a DynamoDB Streams attribute value map to a DynamoDB v2 attribute value map.
	/// </summary>
	/// <param name="streamValues">The streams attribute value map to convert.</param>
	/// <returns>The converted attribute value map, or <see langword="null"/> if the input is null.</returns>
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
			SS = streamValue.SS,
			NS = streamValue.NS,
			BS = streamValue.BS
		};

		if (streamValue.M is { Count: > 0 })
		{
			converted.M = ToAttributeValueMap(streamValue.M);
		}

		if (streamValue.L is { Count: > 0 })
		{
			converted.L = [.. streamValue.L.Select(ToAttributeValue)];
		}

		return converted;
	}
}
