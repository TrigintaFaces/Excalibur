// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch;

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
	internal const string MessageTypeKey = "__MessageType";
	internal const string MessageTypeIsRoutingDefaultKey = "__MessageTypeIsRoutingDefault";
	private const string ContentTypeKey = "__ContentType";
	private const string ReceivedTimestampUtcKey = "__ReceivedTimestampUtc";
	private const string SentTimestampUtcKey = "__SentTimestampUtc";

	/// <summary>
	/// Items key under which the inbox middleware stashes the provider-native
	/// <see cref="IInboxTransactionScope"/> during scoped transactional processing. Shared with the middleware
	/// (which writes it) so both read and write the same key.
	/// </summary>
	internal const string InboxTransactionScopeKey = "Excalibur.Dispatch.Inbox.TransactionScope";

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

	/// <summary>
	/// Gets the provider-native inbox transaction scope for the message currently being processed, or
	/// <see langword="null"/> when the message is not being processed under a scoped transactional inbox store.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// The opaque <see cref="IInboxTransactionScope"/> the inbox handler can enlist its own writes in
	/// (<c>context.GetInboxTransactionScope()?.AsMongoSession()</c> for MongoDB, <c>?.AsCosmosBatch()</c> for
	/// Cosmos DB), or <see langword="null"/> when there is no scoped transaction — meaning the handler runs
	/// under the at-least-once idempotent claim path and should use its own connection/session (its writes are
	/// then NOT atomic with the inbox processed-mark; the handler must be idempotent).
	/// </returns>
	/// <remarks>
	/// This is the consumer enlistment API for exactly-once document-store inbox processing. A
	/// non-<see langword="null"/> scope means the store handed the middleware a native transaction (a MongoDB
	/// session / a Cosmos DB <c>TransactionalBatch</c>); writes the handler enlists on it commit atomically with
	/// the processed-mark. A <see langword="null"/> result is the normal, expected signal that the current store
	/// or configuration does not support scoped transactional processing.
	/// </remarks>
	public static IInboxTransactionScope? GetInboxTransactionScope(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.GetItem<IInboxTransactionScope>(InboxTransactionScopeKey);
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
	/// <remarks>
	/// <see cref="MessageContext"/> keeps this value in a dedicated field and is read from there,
	/// mirroring how correlation and causation are already special-cased for that type: every dispatch
	/// reads and writes the message type, and routing it through the Items dictionary cost a hash lookup
	/// per read and per write on the hot path. An Items entry, if one exists, still wins — the
	/// dictionary stays authoritative for anything that writes the key directly — and every other
	/// <see cref="IMessageContext"/> implementation reads the dictionary exactly as before.
	/// </remarks>
	public static string? GetMessageType(this IMessageContext context) =>
		context is MessageContext messageContext
			? messageContext.TryGetItemFast(MessageTypeKey, out var raw) ? raw as string : messageContext.MessageTypeName
			: context.GetProperty<string>(MessageTypeKey);

	/// <summary>
	/// Sets the message type in Items.
	/// </summary>
	/// <remarks>
	/// See <see cref="GetMessageType"/> for why <see cref="MessageContext"/> stores this in a field.
	/// The value is still surfaced through <see cref="IMessageContext.Items"/> for anything that
	/// enumerates or reads the dictionary.
	/// </remarks>
	public static void SetMessageType(this IMessageContext context, string? value)
	{
		if (context is MessageContext messageContext)
		{
			messageContext.SetMessageTypeFast(value);
			return;
		}

		context.SetProperty(MessageTypeKey, value);
	}

	/// <summary>
	/// Records that <see cref="GetMessageType"/> holds a routing name the framework defaulted in from
	/// the CLR type, rather than a foreign identity received from elsewhere and preserved verbatim.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A context's message type has two possible provenances that share one field: a CloudEvent
	/// identity received from another organisation, which a converter must re-emit verbatim, or a
	/// routing name this dispatcher defaulted in because nothing set one. The two require opposite
	/// handling and are otherwise indistinguishable from the stored string alone.
	/// </para>
	/// <para>
	/// Call this only where the value is being defaulted, mirroring
	/// <see cref="OrderingContextExtensions.MarkOrderingEnforced"/>: mark the case the framework itself
	/// produces, and leave every other case — an inbound receive bridge stamping a foreign identity —
	/// unmarked and untouched.
	/// </para>
	/// </remarks>
	/// <param name="context"> The message context. </param>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static void MarkMessageTypeAsRoutingDefault(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context is MessageContext messageContext)
		{
			messageContext.MarkMessageTypeAsRoutingDefaultFast();
			return;
		}

		context.Items[MessageTypeIsRoutingDefaultKey] = true;
	}

	/// <summary>
	/// Indicates whether <see cref="GetMessageType"/> holds a framework-defaulted routing name rather
	/// than a preserved foreign identity.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns>
	/// <see langword="true"/> if <see cref="MarkMessageTypeAsRoutingDefault"/> was called for this
	/// context; otherwise <see langword="false"/>, meaning the value (if any) was set by something
	/// other than the dispatcher's own default and must be preserved verbatim.
	/// </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="context"/> is <see langword="null"/>. </exception>
	public static bool IsMessageTypeRoutingDefault(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context is MessageContext messageContext)
		{
			return messageContext.TryGetItemFast(MessageTypeIsRoutingDefaultKey, out var item)
				? item is true
				: messageContext.MessageTypeIsRoutingDefault;
		}

		return context.Items.TryGetValue(MessageTypeIsRoutingDefaultKey, out var raw) && raw is true;
	}

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
	/// Gets the routing destination for this message.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The destination, or <see langword="null"/> if not set. </returns>
	public static string? GetDestination(this IMessageContext context) =>
		context.GetProperty<string>(MetadataPropertyKeys.Destination);

	/// <summary>
	/// Sets the routing destination for this message. Uses the canonical
	/// <see cref="MetadataPropertyKeys.Destination"/> key so the value flows through
	/// <c>ExtractMetadata</c> to the persisted message's destination.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="value"> The destination to set. </param>
	public static void SetDestination(this IMessageContext context, string? value) =>
		context.SetProperty(MetadataPropertyKeys.Destination, value);

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
