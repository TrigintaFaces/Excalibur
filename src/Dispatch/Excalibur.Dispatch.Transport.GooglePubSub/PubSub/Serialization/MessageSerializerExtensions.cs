// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Extension methods for <see cref="DispatchJsonSerializer"/> to support Google Cloud Pub/Sub message conversion.
/// </summary>
internal static class MessageSerializerExtensions
{
	/// <summary>
	/// Serializes a message to a <see cref="PubsubMessage"/> with optional attributes.
	/// </summary>
	[RequiresUnreferencedCode("Calls DispatchJsonSerializer.SerializeToUtf8Bytes")]
	public static PubsubMessage SerializeToPubSubMessage<T>(
		this DispatchJsonSerializer serializer,
		T message,
		Dictionary<string, string>? attributes = null)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		var data = serializer.SerializeToUtf8Bytes(message, typeof(T));
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
	public static PubsubMessage SerializeToPubSubMessage(
		this DispatchJsonSerializer serializer,
		object message,
		Dictionary<string, string>? attributes = null)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		var data = serializer.SerializeToUtf8Bytes(message, message.GetType());
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
	[RequiresUnreferencedCode("Calls DispatchJsonSerializer.DeserializeFromBytes")]
	public static T DeserializeFromPubSubMessage<T>(
		this DispatchJsonSerializer serializer,
		ReceivedMessage message)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(message);

		var data = message.Message.Data.ToByteArray();
		var result = (T?)serializer.DeserializeFromBytes(data, typeof(T));
		return result ?? throw new InvalidOperationException(
			$"Failed to deserialize PubSub message to {typeof(T).Name}");
	}
}
