// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IMessageContext" />.
/// </summary>
public static class MessageContextExtensions
{
	// ===== Well-known Items keys =====
	private const string TransportBindingKey = "Excalibur.Dispatch.TransportBinding";
	private const string ValidationResultKey = "__ValidationResult";
	private const string AuthorizationResultKey = "__AuthorizationResult";
	private const string VersionMetadataKey = "__VersionMetadata";
	private const string DesiredVersionKey = "__DesiredVersion";
	private const string MessageVersionKey = "__MessageVersion";
	private const string SerializerVersionKey = "__SerializerVersion";
	private const string ContractVersionKey = "__ContractVersion";
	private const string PartitionKeyKey = "__PartitionKey";
	private const string ReplyToKey = "__ReplyTo";
	private const string MetadataKey = "__Metadata";
	private const string MessageTypeKey = "__MessageType";
	private const string ContentTypeKey = "__ContentType";
	private const string ReceivedTimestampUtcKey = "__ReceivedTimestampUtc";
	private const string SentTimestampUtcKey = "__SentTimestampUtc";

	// ===== Items dictionary helpers (moved from IMessageContext methods) =====

	/// <summary>
	/// Determines whether the Items dictionary contains the specified key.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <param name="key">The key to check for existence.</param>
	/// <returns>True if the key exists; otherwise, false.</returns>
	public static bool ContainsItem(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Items.ContainsKey(key);
	}

	/// <summary>
	/// Gets an item from the Items dictionary, returning null if not found.
	/// </summary>
	/// <typeparam name="T">The type to cast the item to.</typeparam>
	/// <param name="context">The message context.</param>
	/// <param name="key">The key of the item to retrieve.</param>
	/// <returns>The item cast to type T, or null if not found or wrong type.</returns>
	public static T? GetItem<T>(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Items.TryGetValue(key, out var value) && value is T typed ? typed : default;
	}

	/// <summary>
	/// Gets an item from the Items dictionary, returning a default value if not found.
	/// </summary>
	/// <typeparam name="T">The type to cast the item to.</typeparam>
	/// <param name="context">The message context.</param>
	/// <param name="key">The key of the item to retrieve.</param>
	/// <param name="defaultValue">The value to return if the key is not found.</param>
	/// <returns>The item cast to type T, or the default value if not found.</returns>
	public static T GetItem<T>(this IMessageContext context, string key, T defaultValue)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Items.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
	}

	/// <summary>
	/// Removes an item from the Items dictionary.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <param name="key">The key of the item to remove.</param>
	public static void RemoveItem(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Items.Remove(key);
	}

	/// <summary>
	/// Sets or updates an item in the Items dictionary.
	/// </summary>
	/// <typeparam name="T">The type of the value to store.</typeparam>
	/// <param name="context">The message context.</param>
	/// <param name="key">The key to store the value under.</param>
	/// <param name="value">The value to store.</param>
	public static void SetItem<T>(this IMessageContext context, string key, T value)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Items[key] = value!;
	}

	// ===== Property helpers =====

	/// <summary>
	/// Gets or sets a property in the context Items dictionary.
	/// </summary>
	public static void SetProperty<T>(this IMessageContext context, string key, T value)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Items[key] = value!;
	}

	/// <summary>
	/// Gets a property from the context Items dictionary.
	/// </summary>
	public static T? GetProperty<T>(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Items.TryGetValue(key, out var value) && value is T typed ? typed : default;
	}

	/// <summary>
	/// Tries to get a property from the context Items dictionary.
	/// </summary>
	public static bool TryGetProperty<T>(this IMessageContext context, string key, out T? value)
	{
		ArgumentNullException.ThrowIfNull(context);
		value = default;

		if (context.Items.TryGetValue(key, out var itemValue) && itemValue is T typed)
		{
			value = typed;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Removes a property from the context Items dictionary.
	/// </summary>
	public static void RemoveProperty(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Items.Remove(key);
	}

	/// <summary>
	/// Tries to get a value from the context Items dictionary.
	/// </summary>
	public static bool TryGetValue<T>(this IMessageContext context, string key, out T? value)
	{
		ArgumentNullException.ThrowIfNull(context);
		value = default;

		if (context.Items.TryGetValue(key, out var itemValue) && itemValue is T typed)
		{
			value = typed;
			return true;
		}

		return false;
	}

	// ===== Properties that moved from IMessageContext to Items =====

	/// <summary>
	/// Gets the message type from Items.
	/// </summary>
	public static string? GetMessageType(this IMessageContext context) =>
		context.GetProperty<string>(MessageTypeKey);

	/// <summary>
	/// Sets the message type in Items.
	/// </summary>
	public static void SetMessageType(this IMessageContext context, string? value) =>
		context.SetProperty(MessageTypeKey, value);

	/// <summary>
	/// Gets the content type from Items.
	/// </summary>
	public static string? GetContentType(this IMessageContext context) =>
		context.GetProperty<string>(ContentTypeKey);

	/// <summary>
	/// Sets the content type in Items.
	/// </summary>
	public static void SetContentType(this IMessageContext context, string? value) =>
		context.SetProperty(ContentTypeKey, value);

	/// <summary>
	/// Gets the received timestamp from Items.
	/// </summary>
	public static DateTimeOffset? GetReceivedTimestampUtc(this IMessageContext context) =>
		context.GetProperty<DateTimeOffset?>(ReceivedTimestampUtcKey);

	/// <summary>
	/// Sets the received timestamp in Items.
	/// </summary>
	public static void SetReceivedTimestampUtc(this IMessageContext context, DateTimeOffset? value) =>
		context.SetProperty(ReceivedTimestampUtcKey, value);

	/// <summary>
	/// Gets the sent timestamp from Items.
	/// </summary>
	public static DateTimeOffset? GetSentTimestampUtc(this IMessageContext context) =>
		context.GetProperty<DateTimeOffset?>(SentTimestampUtcKey);

	/// <summary>
	/// Sets the sent timestamp in Items.
	/// </summary>
	public static void SetSentTimestampUtc(this IMessageContext context, DateTimeOffset? value) =>
		context.SetProperty(SentTimestampUtcKey, value);

	// ===== Well-known Items properties (existing) =====

	/// <summary>
	/// Gets the validation result.
	/// </summary>
	public static object? ValidationResult(this IMessageContext context) =>
		context.GetProperty<object>(ValidationResultKey);

	/// <summary>
	/// Sets the validation result.
	/// </summary>
	public static void ValidationResult(this IMessageContext context, object? value) =>
		context.SetProperty(ValidationResultKey, value);

	/// <summary>
	/// Gets the authorization result.
	/// </summary>
	public static object? AuthorizationResult(this IMessageContext context) =>
		context.GetProperty<object>(AuthorizationResultKey);

	/// <summary>
	/// Sets the authorization result.
	/// </summary>
	public static void AuthorizationResult(this IMessageContext context, object? value) =>
		context.SetProperty(AuthorizationResultKey, value);

	/// <summary>
	/// Gets the version metadata.
	/// </summary>
	public static object? VersionMetadata(this IMessageContext context) =>
		context.GetProperty<object>(VersionMetadataKey);

	/// <summary>
	/// Sets the version metadata.
	/// </summary>
	public static void VersionMetadata(this IMessageContext context, object? value) =>
		context.SetProperty(VersionMetadataKey, value);

	/// <summary>
	/// Gets the desired version.
	/// </summary>
	public static string? DesiredVersion(this IMessageContext context) =>
		context.GetProperty<string>(DesiredVersionKey);

	/// <summary>
	/// Sets the desired version.
	/// </summary>
	public static void DesiredVersion(this IMessageContext context, string? value) =>
		context.SetProperty(DesiredVersionKey, value);

	/// <summary>
	/// Gets the message version.
	/// </summary>
	public static string? MessageVersion(this IMessageContext context) =>
		context.GetProperty<string>(MessageVersionKey);

	/// <summary>
	/// Sets the message version.
	/// </summary>
	public static void MessageVersion(this IMessageContext context, string? value) =>
		context.SetProperty(MessageVersionKey, value);

	/// <summary>
	/// Gets the serializer version.
	/// </summary>
	public static string? SerializerVersion(this IMessageContext context) =>
		context.GetProperty<string>(SerializerVersionKey);

	/// <summary>
	/// Sets the serializer version.
	/// </summary>
	public static void SerializerVersion(this IMessageContext context, string? value) =>
		context.SetProperty(SerializerVersionKey, value);

	/// <summary>
	/// Gets the contract version.
	/// </summary>
	public static string? ContractVersion(this IMessageContext context) =>
		context.GetProperty<string>(ContractVersionKey);

	/// <summary>
	/// Sets the contract version.
	/// </summary>
	public static void ContractVersion(this IMessageContext context, string? value) =>
		context.SetProperty(ContractVersionKey, value);

	/// <summary>
	/// Gets the partition key from Items (alias for routing feature partition key).
	/// </summary>
	public static string? PartitionKey(this IMessageContext context) =>
		context.GetProperty<string>(PartitionKeyKey);

	/// <summary>
	/// Sets the partition key in Items.
	/// </summary>
	public static void PartitionKey(this IMessageContext context, string? value) =>
		context.SetProperty(PartitionKeyKey, value);

	/// <summary>
	/// Gets the reply-to address.
	/// </summary>
	public static string? ReplyTo(this IMessageContext context) =>
		context.GetProperty<string>(ReplyToKey);

	/// <summary>
	/// Sets the reply-to address.
	/// </summary>
	public static void ReplyTo(this IMessageContext context, string? value) =>
		context.SetProperty(ReplyToKey, value);

	/// <summary>
	/// Gets metadata dictionary.
	/// </summary>
	public static IDictionary<string, object>? Metadata(this IMessageContext context) =>
		context.GetProperty<IDictionary<string, object>>(MetadataKey);

	/// <summary>
	/// Sets metadata dictionary.
	/// </summary>
	public static void Metadata(this IMessageContext context, IDictionary<string, object>? value) =>
		context.SetProperty(MetadataKey, value);

	// ===== Transport binding =====

	/// <summary>
	/// Gets the transport binding that received this message.
	/// </summary>
	public static ITransportBinding? TransportBinding(this IMessageContext context) =>
		context.GetProperty<ITransportBinding>(TransportBindingKey);

	/// <summary>
	/// Gets a value indicating whether this message was received via a transport adapter.
	/// </summary>
	public static bool HasTransportBinding(this IMessageContext context) =>
		context.TransportBinding() != null;
}
