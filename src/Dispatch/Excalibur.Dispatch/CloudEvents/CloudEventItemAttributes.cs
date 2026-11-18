// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Projects message-context items onto CloudEvents extension attributes.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every writer that puts context items on a CloudEvent, because the rules are a property of
/// the CloudEvents specification rather than of any one writer, and a rule restated per writer is a rule
/// that diverges per writer. Two writers already had: one sanitized and prefixed its keys, the other
/// wrote the raw key straight onto the event.
/// </para>
/// <para>
/// <strong>The item collection is public, so no writer may assume its producer was disciplined.</strong>
/// A caller can place any key and any value there directly, so every rule below is enforced here rather
/// than trusted to the source. In particular a key that merely looks externally sourced proves nothing:
/// prefixes are forgeable by any caller and are never treated as evidence of provenance.
/// </para>
/// </remarks>
internal static class CloudEventItemAttributes
{
	/// <summary>
	/// The CloudEvents v1.0 context attributes, plus the extension attributes this framework sets itself.
	/// </summary>
	/// <remarks>
	/// A composed item attribute that landed on one of these would overwrite a value the encoder had
	/// already set from the message context — silently, and after the fact, because items are written
	/// last. Overwriting <c>correlationid</c> was a live defect: one writer set it from the context and
	/// then let an item of the same name replace it ten lines later.
	/// </remarks>
	private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
	{
		// CloudEvents v1.0 §3 context attributes.
		"data", // Reserved payload name, not an extension attribute.
		"specversion",
		"id",
		"source",
		"type",
		"datacontenttype",
		"dataschema",
		"subject",
		"time",

		// Extension attributes the encoders set from the context before items are applied.
		"correlationid",
		"causationid",
		"traceparent",
		"tracestate",
	};

	/// <summary>
	/// Writes the items as extension attributes, in a deterministic order, skipping any that cannot be
	/// represented or that would collide with a reserved name.
	/// </summary>
	/// <param name="cloudEvent"> The event to write onto. </param>
	/// <param name="items"> The context items. </param>
	/// <param name="namePrefix">
	/// An optional prefix composed onto each attribute name. It is itself reduced to the characters the
	/// specification allows, so a caller cannot compose an invalid name through it.
	/// </param>
	/// <remarks>
	/// Items are applied in ordinal key order. The collection is usually a dictionary, whose enumeration
	/// order is unspecified, so without this the emitted attribute set would be a function of hash order
	/// as soon as two keys sanitize to the same name — the later writer would win arbitrarily, and two
	/// encodings of the same message could differ with nothing downstream able to tell.
	/// </remarks>
	public static void Apply(
		CloudEvent cloudEvent,
		IEnumerable<KeyValuePair<string, string>> items,
		string? namePrefix = null)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		if (items is null)
		{
			return;
		}

		var prefix = Sanitize(namePrefix ?? string.Empty);

		foreach (var item in items.OrderBy(i => i.Key, StringComparer.Ordinal))
		{
			var name = prefix + Sanitize(item.Key);

			// A key with no representable characters would compose to the bare prefix, which is either
			// empty (invalid) or shared by every such key (a silent collision). Neither is an attribute.
			if (name.Length == 0 || name == prefix || ReservedNames.Contains(name))
			{
				continue;
			}

			cloudEvent.SetAttributeFromString(name, item.Value ?? string.Empty);
		}
	}

	/// <summary>
	/// Reduces an arbitrary key to a valid CloudEvents attribute-name fragment: lowercase ASCII letters
	/// and digits only (CloudEvents v1.0 §3.1).
	/// </summary>
	/// <param name="key"> The key to reduce. </param>
	/// <returns> The reduced fragment, which may be empty. </returns>
	/// <remarks>
	/// The specification's grammar is not ours to widen: an attribute name outside it makes the event
	/// unreadable to a conformant consumer, and the CNCF SDK refuses to construct one, so an unsanitized
	/// key does not produce a badly-named attribute — it throws while encoding a message that was
	/// otherwise fine.
	/// </remarks>
	private static string Sanitize(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return string.Empty;
		}

		var builder = new StringBuilder(key.Length);
		foreach (var ch in key)
		{
			if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
			{
				_ = builder.Append(ch);
			}
			else if (ch is >= 'A' and <= 'Z')
			{
				_ = builder.Append(char.ToLowerInvariant(ch));
			}
		}

		return builder.ToString();
	}
}
