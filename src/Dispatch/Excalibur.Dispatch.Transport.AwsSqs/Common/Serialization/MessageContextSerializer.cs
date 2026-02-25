// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS-specific adapter for message context serialization that converts between the generic Dictionary format and AWS
/// MessageAttributeValue format while preserving all context information.
/// </summary>
/// <remarks>
/// This class delegates to the provider-agnostic MessageContextSerializer in Abstractions and adapts the results for AWS SQS
/// MessageAttributeValue format. It provides optimized handling of numeric attributes using AWS's Number data type for better performance
/// and type safety.
/// </remarks>
public static class MessageContextSerializer
{
	/// <summary>
	/// Serializes the complete message context into SQS message attributes.
	/// </summary>
	/// <param name="context"> The message context to serialize. </param>
	/// <param name="messageAttributes"> The SQS message attributes dictionary to populate. </param>
	/// <remarks>
	/// This method uses the provider-agnostic serializer from Abstractions and converts the result to AWS MessageAttributeValue format with
	/// optimized Number data types for numeric fields.
	/// </remarks>
	public static void SerializeToMessageAttributes(
		IMessageContext context,
		Dictionary<string, MessageAttributeValue> messageAttributes)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(messageAttributes);

		// Use the enhanced Abstractions serializer
		var attributesDictionary = Transport.MessageContextSerializer.SerializeToDictionary(context);

		// Convert to AWS MessageAttributeValue format with optimized data types
		ConvertToMessageAttributes(attributesDictionary, messageAttributes);
	}

	/// <summary>
	/// Deserializes message attributes back into a complete message context.
	/// </summary>
	/// <param name="messageAttributes"> The SQS message attributes to deserialize. </param>
	/// <param name="requestServices"> The service provider for the context. </param>
	/// <returns> A fully populated message context. </returns>
	public static IMessageContext DeserializeFromMessageAttributes(
		Dictionary<string, MessageAttributeValue> messageAttributes,
		IServiceProvider requestServices)
	{
		ArgumentNullException.ThrowIfNull(messageAttributes);
		ArgumentNullException.ThrowIfNull(requestServices);

		// Convert AWS MessageAttributeValue format to string dictionary
		var attributesDictionary = ConvertFromMessageAttributes(messageAttributes);

		// Use the enhanced Abstractions deserializer
		return Transport.MessageContextSerializer.DeserializeFromDictionary(attributesDictionary, requestServices);
	}

	/// <summary>
	/// Converts a generic string dictionary to AWS MessageAttributeValue format with optimized data types. Numeric values are stored with
	/// AWS Number data type for better performance and type safety.
	/// </summary>
	/// <param name="attributesDictionary"> The source dictionary with string values. </param>
	/// <param name="messageAttributes"> The target AWS MessageAttributeValue dictionary. </param>
	private static void ConvertToMessageAttributes(
		Dictionary<string, string> attributesDictionary,
		Dictionary<string, MessageAttributeValue> messageAttributes)
	{
		// Define which keys should be treated as numeric for optimization
		var numericKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"X-DeliveryCount",
			"X-Priority",
			"X-DesiredVersion",
			"X-SentTimestamp",
			"X-ScheduledDelivery",
			"X-TimeToLive",
		};

		foreach (var kvp in attributesDictionary)
		{
			// Use Number data type for numeric fields to optimize AWS SQS performance
			if (numericKeys.Contains(kvp.Key) && long.TryParse(kvp.Value, out _))
			{
				messageAttributes[kvp.Key] = new MessageAttributeValue { StringValue = kvp.Value, DataType = "Number" };
			}
			else
			{
				messageAttributes[kvp.Key] = new MessageAttributeValue { StringValue = kvp.Value, DataType = "String" };
			}
		}
	}

	/// <summary>
	/// Converts AWS MessageAttributeValue format back to a generic string dictionary. Both String and Number data types are handled appropriately.
	/// </summary>
	/// <param name="messageAttributes"> The AWS MessageAttributeValue dictionary. </param>
	/// <returns> A generic string dictionary suitable for the Abstractions deserializer. </returns>
	private static Dictionary<string, string> ConvertFromMessageAttributes(
		Dictionary<string, MessageAttributeValue> messageAttributes)
	{
		var result = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var kvp in messageAttributes)
		{
			if (!string.IsNullOrEmpty(kvp.Value.StringValue))
			{
				result[kvp.Key] = kvp.Value.StringValue;
			}
		}

		return result;
	}
}
