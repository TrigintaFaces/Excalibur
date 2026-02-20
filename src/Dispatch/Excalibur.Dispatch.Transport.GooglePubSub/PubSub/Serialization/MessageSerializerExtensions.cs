// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Extension methods for <see cref="IMessageSerializer"/> to support Google Cloud Pub/Sub message conversion.
/// </summary>
internal static class MessageSerializerExtensions
{
	/// <summary>
	/// Serializes a message to a <see cref="PubsubMessage"/> with optional attributes.
	/// </summary>
	/// <typeparam name="T">The type of message to serialize.</typeparam>
	/// <param name="serializer">The message serializer.</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="attributes">Optional message attributes.</param>
	/// <returns>A <see cref="PubsubMessage"/> containing the serialized data.</returns>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IMessageSerializer.Serialize<T>(T)")]
	public static PubsubMessage SerializeToPubSubMessage<T>(
		this IMessageSerializer serializer,
		T message,
		Dictionary<string, string>? attributes = null)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		var data = serializer.Serialize(message);
		var pubsubMessage = new PubsubMessage
		{
			Data = ByteString.CopyFrom(data),
		};

		if (attributes is not null)
		{
			foreach (var (key, value) in attributes)
			{
				pubsubMessage.Attributes[key] = value;
			}
		}

		return pubsubMessage;
	}

	/// <summary>
	/// Serializes a non-generic message to a <see cref="PubsubMessage"/> with optional attributes.
	/// </summary>
	/// <param name="serializer">The message serializer.</param>
	/// <param name="message">The message to serialize.</param>
	/// <param name="attributes">Optional message attributes.</param>
	/// <returns>A <see cref="PubsubMessage"/> containing the serialized data.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the operation fails.</exception>
	[RequiresDynamicCode("Calls System.Reflection.MethodInfo.MakeGenericMethod(params Type[])")]
	[RequiresUnreferencedCode("Calls System.Type.GetMethod(String)")]
	public static PubsubMessage SerializeToPubSubMessage(
		this IMessageSerializer serializer,
		object message,
		Dictionary<string, string>? attributes = null)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		// Use reflection to call the generic Serialize method
		var messageType = message.GetType();
		var serializeMethod = typeof(IMessageSerializer).GetMethod(nameof(IMessageSerializer.Serialize))
			?? throw new InvalidOperationException($"Could not find {nameof(IMessageSerializer.Serialize)} method");
		var genericMethod = serializeMethod.MakeGenericMethod(messageType);
		var data = (byte[])(genericMethod.Invoke(serializer, [message])
			?? throw new InvalidOperationException("Serialization returned null"));

		var pubsubMessage = new PubsubMessage
		{
			Data = ByteString.CopyFrom(data),
		};

		if (attributes is not null)
		{
			foreach (var (key, value) in attributes)
			{
				pubsubMessage.Attributes[key] = value;
			}
		}

		return pubsubMessage;
	}

	/// <summary>
	/// Deserializes a <see cref="ReceivedMessage"/> to the specified type.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The message serializer.</param>
	/// <param name="message">The received message containing the serialized data.</param>
	/// <returns>The deserialized message.</returns>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IMessageSerializer.Deserialize<T>(Byte[])")]
	public static T DeserializeFromPubSubMessage<T>(
		this IMessageSerializer serializer,
		ReceivedMessage message)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		var data = message.Message.Data.ToByteArray();
		return serializer.Deserialize<T>(data);
	}
}
