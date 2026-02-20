// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IMessageContext" />.
/// </summary>
public static class MessageContextExtensions
{
	/// <summary>
	/// Constants for well-known context properties.
	/// </summary>
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

	/// <summary>
	/// Gets or sets a property in the context.
	/// </summary>
	/// <typeparam name="T"> The type of the property. </typeparam>
	/// <param name="context"> The message context. </param>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value. </param>
	public static void SetProperty<T>(this IMessageContext context, string key, T value)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.Properties != null)
		{
			context.Properties[key] = value;
		}
		else if (context.Items != null)
		{
			context.Items[key] = value!;
		}
	}

	/// <summary>
	/// Gets a property from the context.
	/// </summary>
	/// <typeparam name="T"> The type of the property. </typeparam>
	/// <param name="context"> The message context. </param>
	/// <param name="key"> The property key. </param>
	/// <returns> The property value or default. </returns>
	public static T? GetProperty<T>(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.Properties?.TryGetValue(key, out var propValue) == true)
		{
			return propValue is T typed ? typed : default;
		}

		if (context.Items?.TryGetValue(key, out var itemValue) == true)
		{
			return itemValue is T typedItem ? typedItem : default;
		}

		return default;
	}

	/// <summary>
	/// Tries to get a property from the context.
	/// </summary>
	/// <typeparam name="T"> The type of the property. </typeparam>
	/// <param name="context"> The message context. </param>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value if found. </param>
	/// <returns> True if the property was found; otherwise, false. </returns>
	public static bool TryGetProperty<T>(this IMessageContext context, string key, out T? value)
	{
		ArgumentNullException.ThrowIfNull(context);

		value = default;

		if (context.Properties?.TryGetValue(key, out var propValue) == true && propValue is T typed)
		{
			value = typed;
			return true;
		}

		if (context.Items?.TryGetValue(key, out var itemValue) == true && itemValue is T typedItem)
		{
			value = typedItem;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Removes a property from the context.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="key"> The property key. </param>
	public static void RemoveProperty(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);

		_ = context.Properties?.Remove(key);
		_ = context.Items?.Remove(key);
	}

	/// <summary>
	/// Tries to get a value from the context Items dictionary.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="context"> The message context. </param>
	/// <param name="key"> The key. </param>
	/// <param name="value"> The value if found. </param>
	/// <returns> True if the value was found; otherwise, false. </returns>
	public static bool TryGetValue<T>(this IMessageContext context, string key, out T? value)
	{
		ArgumentNullException.ThrowIfNull(context);

		value = default;

		if (context.Items?.TryGetValue(key, out var itemValue) == true && itemValue is T typed)
		{
			value = typed;
			return true;
		}

		return false;
	}

	// Well-known properties

	/// <summary>
	/// Gets the validation result.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The validation result or null if not set. </returns>
	public static object? ValidationResult(this IMessageContext context) =>
		context.GetProperty<object>(ValidationResultKey);

	/// <summary>
	/// Sets the validation result.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void ValidationResult(this IMessageContext context, object? value) =>
		context.SetProperty(ValidationResultKey, value);

	/// <summary>
	/// Gets the authorization result.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The authorization result or null if not set. </returns>
	public static object? AuthorizationResult(this IMessageContext context) =>
		context.GetProperty<object>(AuthorizationResultKey);

	/// <summary>
	/// Sets the authorization result.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void AuthorizationResult(this IMessageContext context, object? value) =>
		context.SetProperty(AuthorizationResultKey, value);

	/// <summary>
	/// Gets or sets the version metadata.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The version metadata or null if not set. </returns>
	public static object? VersionMetadata(this IMessageContext context) =>
		context.GetProperty<object>(VersionMetadataKey);

	/// <summary>
	/// Sets the version metadata.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void VersionMetadata(this IMessageContext context, object? value) =>
		context.SetProperty(VersionMetadataKey, value);

	/// <summary>
	/// Gets or sets the desired version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The desired version or null if not set. </returns>
	public static string? DesiredVersion(this IMessageContext context) =>
		context.GetProperty<string>(DesiredVersionKey);

	/// <summary>
	/// Sets the desired version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void DesiredVersion(this IMessageContext context, string? value) =>
		context.SetProperty(DesiredVersionKey, value);

	/// <summary>
	/// Gets or sets the message version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The message version or null if not set. </returns>
	public static string? MessageVersion(this IMessageContext context) =>
		context.GetProperty<string>(MessageVersionKey);

	/// <summary>
	/// Sets the message version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void MessageVersion(this IMessageContext context, string? value) =>
		context.SetProperty(MessageVersionKey, value);

	/// <summary>
	/// Gets or sets the serializer version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The serializer version or null if not set. </returns>
	public static string? SerializerVersion(this IMessageContext context) =>
		context.GetProperty<string>(SerializerVersionKey);

	/// <summary>
	/// Sets the serializer version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void SerializerVersion(this IMessageContext context, string? value) =>
		context.SetProperty(SerializerVersionKey, value);

	/// <summary>
	/// Gets or sets the contract version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The contract version or null if not set. </returns>
	public static string? ContractVersion(this IMessageContext context) =>
		context.GetProperty<string>(ContractVersionKey);

	/// <summary>
	/// Sets the contract version.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void ContractVersion(this IMessageContext context, string? value) =>
		context.SetProperty(ContractVersionKey, value);

	/// <summary>
	/// Gets or sets the partition key.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The partition key or null if not set. </returns>
	public static string? PartitionKey(this IMessageContext context) =>
		context.GetProperty<string>(PartitionKeyKey);

	/// <summary>
	/// Sets the partition key.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void PartitionKey(this IMessageContext context, string? value) =>
		context.SetProperty(PartitionKeyKey, value);

	/// <summary>
	/// Gets or sets the reply-to address.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The reply-to address or null if not set. </returns>
	public static string? ReplyTo(this IMessageContext context) =>
		context.GetProperty<string>(ReplyToKey);

	/// <summary>
	/// Sets the reply-to address.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void ReplyTo(this IMessageContext context, string? value) =>
		context.SetProperty(ReplyToKey, value);

	/// <summary>
	/// Gets or sets metadata dictionary.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The metadata dictionary or null if not set. </returns>
	public static IDictionary<string, object>? Metadata(this IMessageContext context) =>
		context.GetProperty<IDictionary<string, object>>(MetadataKey);

	/// <summary>
	/// Sets metadata dictionary.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The property value. </param>
	public static void Metadata(this IMessageContext context, IDictionary<string, object>? value) =>
		context.SetProperty(MetadataKey, value);

	// Transport binding properties

	/// <summary>
	/// Gets the transport binding that received this message.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// The transport binding if the message was received via a transport adapter;
	/// otherwise, <see langword="null"/> for directly dispatched messages.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This property is set by the <see cref="ITransportContextProvider"/>
	/// at the beginning of message dispatch, before any middleware executes.
	/// </para>
	/// <para>
	/// Use this to access transport-specific configuration or to determine
	/// the origin of the message for routing or profiling decisions.
	/// </para>
	/// </remarks>
	public static ITransportBinding? TransportBinding(this IMessageContext context) =>
		context.GetProperty<ITransportBinding>(TransportBindingKey);

	/// <summary>
	/// Gets a value indicating whether this message was received via a transport adapter.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// <see langword="true"/> if the message was received via a transport adapter;
	/// <see langword="false"/> if the message was dispatched directly (in-process).
	/// </returns>
	public static bool HasTransportBinding(this IMessageContext context) =>
		context.TransportBinding() != null;
}
