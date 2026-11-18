// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.ObjectModel;

using Excalibur.Dispatch.Features;
using System.Text;

namespace Excalibur.Dispatch;

/// <summary>
/// Carries named values beyond the process boundary, alongside a message.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IMessageContext.Items" /> is request-local: nothing in this framework serializes it,
/// persists it, transmits it or emits it to a telemetry backend. Use these methods for the values that
/// must travel.
/// </para>
/// <para>
/// The split is the one <see cref="System.Diagnostics.Activity" /> already makes, for the same reason:
/// <see cref="System.Diagnostics.Activity.Tags" /> stay in the process and
/// <see cref="System.Diagnostics.Activity.Baggage" /> is propagated, and they are two collections rather
/// than one collection with a naming convention. A single bag whose entries sometimes cross a trust
/// boundary cannot be read safely, because whether a given write leaves the process depends on a key
/// spelling rather than on the call.
/// </para>
/// <para>
/// Propagation is <strong>explicit and per-value</strong>. There is no option that propagates
/// <see cref="IMessageContext.Items" /> wholesale, and that absence is deliberate: a switch would make the
/// meaning of a write to <c>Items</c> depend on deployment configuration, so a consumer reading their own
/// handler could not tell what the line does.
/// </para>
/// </remarks>
public static class MessageContextPropagationExtensions
{
	/// <summary>
	/// The largest a propagated key may be, in UTF-8 bytes. This is the strictest limit any shipped
	/// transport applies to an attribute name, and that transport documents it as a fixed constraint
	/// that cannot be raised or lowered by configuration.
	/// </summary>
	internal const int MaxPropagatedKeyBytes = 256;

	/// <summary>
	/// The largest a propagated value may be, in UTF-8 bytes. As with the key limit, this is the
	/// strictest bound across the shipped transports and is not configurable at that broker. A value
	/// over it is REFUSED rather than shortened: a truncated value reads downstream as a whole one.
	/// </summary>
	internal const int MaxPropagatedValueBytes = 1024;

	/// <summary>
	/// The largest number of propagated items one message may carry. The strictest shipped transport
	/// rejects the whole message when a publish exceeds its attribute count, so the bound is applied
	/// where the item is set rather than where the message is sent.
	/// </summary>
	internal const int MaxPropagatedItems = 100;

	/// <summary>
	/// Marks a value to travel with the message beyond this process.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <param name="key">The name the value travels under. Rendered per binding by each transport.</param>
	/// <param name="value">The value. Already its own wire form.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="context" />, <paramref name="key" /> or <paramref name="value" /> is
	/// <see langword="null" />.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="key" /> is empty or whitespace; or names a header an outbox store supplies as its
	/// own envelope framing; or <paramref name="key" /> exceeds 256 UTF-8 bytes; or
	/// <paramref name="value" /> exceeds 1024 UTF-8 bytes; or this context already carries 100 propagated
	/// items and <paramref name="key" /> is not one of them.
	/// </exception>
	/// <remarks>
	/// <para>
	/// A later call with the same <paramref name="key" /> replaces the earlier value.
	/// </para>
	/// <para>
	/// <strong>Bounded, and the bound REFUSES rather than truncates:</strong> a key of at most 256 UTF-8
	/// bytes, a value of at most 1024 UTF-8 bytes, and at most 100 propagated items on one message.
	/// Overwriting a key you already set is always allowed, since it does not change the count. The sizes
	/// are measured in UTF-8 bytes and not in characters, because that is the unit every transport limit
	/// is expressed in — a value of 600 multi-byte characters can exceed the byte limit.
	/// </para>
	/// <para>
	/// <strong>Why it refuses instead of shortening, which is the decisive part rather than a stylistic
	/// one:</strong> a value this framework truncated would be read downstream as though it were whole,
	/// and nothing there could tell. An exception is loud and recoverable at the line that caused it; a
	/// quietly shortened value is neither.
	/// </para>
	/// <para>
	/// <strong>Why the bound lives here and not at the transport.</strong> A marked value is set once and
	/// may be forwarded to any transport, and to several, so the only sound bound is the intersection of
	/// what every transport accepts — applied where your own code is on the stack and the offending value
	/// is attributable. The numbers above are the strictest limits across the supported transports, and
	/// that transport documents them as fixed constraints which cannot be raised by configuration.
	/// Enforcing them at the sink instead would mean the same code works against one broker and fails
	/// against another, discovered in production: brokers reject the whole message rather than the
	/// offending header, so an oversized value becomes an undeliverable staged row that retries
	/// indefinitely, which is a stall you cannot see.
	/// </para>
	/// <para>
	/// This is a deliberate deviation from
	/// <see cref="System.Diagnostics.Activity.AddBaggage(string, string?)" />, which applies no bound.
	/// The shape is the same and the destination is not: baggage stays within a trace, while a marked
	/// value is published as a broker attribute under a hard, non-negotiable limit. Matching the surface
	/// is right; inheriting the unboundedness would move the failure to a place the caller cannot see.
	/// </para>
	/// <para>
	/// Still validate a value that came from your caller before marking it. These bounds stop an
	/// oversized value, not an untrusted one, and this method's intended use means the value is
	/// frequently theirs rather than yours.
	/// </para>
	/// </remarks>
	public static void SetPropagatedItem(this IMessageContext context, string key, string value)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);

		// Refused at the line the consumer wrote, not resolved by precedence at the sink. An outbox
		// store supplies these five as its own envelope framing, so a marked value of the same name
		// reached the broker on seven providers and was silently replaced on two -- the same code with a
		// different result per provider. Failing here makes that inexpressible and says so once.
		foreach (var reserved in OutboundMessage.ReservedHeaderNames)
		{
			if (string.Equals(key, reserved, StringComparison.Ordinal))
			{
				throw new ArgumentException(
					$"'{key}' is reserved: an outbox store supplies it as the message's own envelope framing, "
					+ "so a propagated value of that name would be overwritten on some providers and not "
					+ "others. Choose a different name, or set the context field this header is derived from.",
					nameof(key));
			}
		}

		// BOUNDED HERE BECAUSE THE BROKER REJECTS THE MESSAGE, NOT THE HEADER. A propagated item is set
		// once and may be forwarded to any transport, and to several, so the only sound bound is the
		// INTERSECTION of what every shipped transport accepts -- applied where the consumer's own code
		// is on the stack and the offending value is attributable. Enforcing it per-transport instead
		// means the same consumer code works on one broker and fails on another, discovered in
		// production, as a staged outbox row that retries forever.
		//
		// The numbers are Google Pub/Sub's, which are the tightest across the shipped set and which its
		// own quotas page classes as fixed constraints that cannot be raised or lowered: at most 100
		// attributes, keys at most 256 bytes, values at most 1024 bytes. gRPC is looser but worse-behaved
		// -- metadata counts against the HTTP/2 header list size, whose default varies by language and
		// version, and requests between the soft and absolute limits are rejected as a random sample
		// while the server observes nothing. A bound that satisfies Pub/Sub satisfies the rest.
		//
		// REFUSING, NEVER TRUNCATING. A silently shortened value reads downstream as whole, which is the
		// catastrophic class here: nothing learns that it was cut. Throwing is loud and recoverable.
		//
		// Measured in BYTES, not characters, because every limit above is a byte limit: a value of 1024
		// multi-byte characters passes a length check and is refused by the broker.
		var keyBytes = Encoding.UTF8.GetByteCount(key);
		if (keyBytes > MaxPropagatedKeyBytes)
		{
			throw new ArgumentException(
				$"The propagated key is {keyBytes} UTF-8 bytes, over the {MaxPropagatedKeyBytes}-byte limit "
				+ "that the strictest shipped transport applies to an attribute name. The limit is a fixed "
				+ "constraint of that broker and cannot be raised by configuration, so an oversized name "
				+ "would fail the publish rather than being shortened. Use a shorter name.",
				nameof(key));
		}

		if (Encoding.UTF8.GetByteCount(value) is var valueBytes && valueBytes > MaxPropagatedValueBytes)
		{
			throw new ArgumentException(
				$"The propagated value for '{key}' is {valueBytes} UTF-8 bytes, over the "
				+ $"{MaxPropagatedValueBytes}-byte limit that the strictest shipped transport applies to an "
				+ "attribute value. It is refused here rather than truncated, because a shortened value "
				+ "reads as a whole one downstream, and rather than at the broker, because there it fails "
				+ "the whole message. Carry the payload in the message body and propagate a reference.",
				nameof(value));
		}

		var items = GetOrCreatePropagationFeature(context).Items;

		// Only a NEW key grows the set, so overwriting an existing one is always allowed -- a consumer
		// correcting a value they already set must not be refused for a count they are not changing.
		if (!items.ContainsKey(key) && items.Count >= MaxPropagatedItems)
		{
			throw new ArgumentException(
				$"This context already carries {items.Count} propagated items, the limit the strictest "
				+ "shipped transport applies to the number of attributes on one message. Adding another "
				+ "would fail the publish. Overwriting one of the existing keys is still allowed.",
				nameof(key));
		}

		items[key] = value;
	}

	/// <summary>
	/// Removes one value marked to travel with the message.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <param name="key">The exact, case-sensitive propagated key.</param>
	/// <returns>Whether a marked value was removed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context" /> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="key" /> is null, empty, or whitespace.</exception>
	/// <remarks>
	/// Only explicitly propagated values are affected. Request-local items remain unchanged.
	/// Removing an absent key does not allocate a propagation collection. This method does not
	/// authorize the removal or validate the meaning of the value.
	/// </remarks>
	public static bool RemovePropagatedItem(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		return context.Features.TryGetValue(typeof(MessagePropagationFeature), out var value)
			&& value is MessagePropagationFeature feature
			&& feature.Items.Remove(key);
	}

	/// <summary>
	/// Gets the values marked to travel with the message.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <returns>
	/// The propagated entries, or an empty dictionary when none were marked. Never <see langword="null" />.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
	/// <remarks>
	/// The returned dictionary is a live, read-only view of the context's own collection rather than a
	/// copy, so a later <see cref="SetPropagatedItem" /> is visible through it and a sink on a
	/// per-message path allocates nothing to read it. It is read-only <em>to its caller</em>, not merely
	/// in its declared type: mutating it through any cast throws. A sink that must hold the set across an
	/// await should copy it.
	/// </remarks>
	public static IReadOnlyDictionary<string, string> GetPropagatedItems(this IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Features.TryGetValue(typeof(MessagePropagationFeature), out var value)
			&& value is MessagePropagationFeature feature
				? feature.View
				: EmptyPropagatedItems;
	}

	/// <summary>
	/// Gets a value marked to travel with the message.
	/// </summary>
	/// <param name="context">The message context.</param>
	/// <param name="key">The name the value travels under.</param>
	/// <returns>The value, or <see langword="null" /> when the key was not marked.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="context" /> or <paramref name="key" /> is <see langword="null" />.
	/// </exception>
	public static string? GetPropagatedItem(this IMessageContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(key);

		return GetPropagatedItems(context).TryGetValue(key, out var value) ? value : null;
	}

	/// <summary>
	/// Copies the propagated entries of <paramref name="source" /> into <paramref name="destination" />.
	/// </summary>
	/// <remarks>
	/// The copy is into a fresh collection owned by <paramref name="destination" />, so a later write to
	/// either context is invisible to the other. A shared reference here would make a child's write mutate
	/// its parent retroactively, after a sink may already have read the parent's set.
	/// </remarks>
	internal static void CopyPropagatedItems(IMessageContext source, IMessageContext destination)
	{
		var entries = GetPropagatedItems(source);
		if (entries.Count == 0)
		{
			return;
		}

		var target = GetOrCreatePropagationFeature(destination).Items;
		foreach (var entry in entries)
		{
			target[entry.Key] = entry.Value;
		}
	}

	// ReadOnlyDictionary.Empty, never a Dictionary. This instance is handed to EVERY context that has
	// marked nothing, so a Dictionary here would be a process-global shared mutable: one cast to
	// IDictionary from any assembly would add an entry that every later unmarked context in the process
	// reports as propagated, and FromContext would stage it onto messages belonging to other tenants
	// until the process restarted. The empty case is the one where a shared instance is most tempting
	// and most dangerous.
	private static readonly IReadOnlyDictionary<string, string> EmptyPropagatedItems =
		ReadOnlyDictionary<string, string>.Empty;

	private static MessagePropagationFeature GetOrCreatePropagationFeature(IMessageContext context)
	{
		var features = context.Features;
		if (features.TryGetValue(typeof(MessagePropagationFeature), out var value)
			&& value is MessagePropagationFeature existing)
		{
			return existing;
		}

		var created = new MessagePropagationFeature();
		features[typeof(MessagePropagationFeature)] = created;

		// Read it back. IMessageContext.Features is documented as returning the same instance on every
		// call, but that is a property of the IMPLEMENTATION and a consumer type returning a fresh
		// collection per call compiles and satisfies the signature. Without this check the write above
		// lands in a discarded dictionary and EVERY marked value is lost with nothing raised and nothing
		// logged -- silence, which is this framework's catastrophic class. One lookup, on the
		// first mark per context only, converts that into a failure at the consumer's own line.
		if (!ReferenceEquals(context.Features, features)
			|| !context.Features.TryGetValue(typeof(MessagePropagationFeature), out var readBack)
			|| !ReferenceEquals(readBack, created))
		{
			throw new InvalidOperationException(
				$"'{context.GetType().Name}.Features' did not return the collection it was just written to. "
				+ "IMessageContext.Features must return the same instance on every call, for the life of "
				+ "the context; an implementation returning a fresh collection per call cannot carry "
				+ "propagated values and would lose them silently.");
		}

		return created;
	}
}
