// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Features;

/// <summary>
/// Holds the values a message context carries beyond the process boundary.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <see langword="internal" />, and reached only through
/// <see cref="MessageContextPropagationExtensions" />. <see cref="IMessageContext.Features" /> is a public
/// <see cref="IDictionary{TKey,TValue}" /> keyed by <see cref="Type" />, so a feature whose type a consumer
/// can name is a feature a consumer can replace wholesale. An internal type cannot be named from outside
/// this assembly, which makes that substitution inexpressible rather than merely discouraged.
/// </para>
/// <para>
/// The value type is <see cref="string" /> rather than <see cref="object" /> because there is no total
/// render function for <see cref="object" />: every sink would have to invent one, and the ones that did
/// rendered a dictionary as its own type name and reported success. A string is already its own wire form,
/// which is what W3C baggage, broker headers and CloudEvents extension attributes each require.
/// </para>
/// </remarks>
internal sealed class MessagePropagationFeature
{
	/// <summary>
	/// Gets the propagated entries for writing, inside this assembly.
	/// </summary>
	/// <remarks>
	/// Ordinal rather than case-insensitive, matching <see cref="System.Diagnostics.Activity.Baggage" />:
	/// the bindings these entries render into disagree about case folding, so folding here would silently
	/// pick one broker's rule for all of them.
	/// </remarks>
	internal Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);

	private ReadOnlyDictionary<string, string>? _view;

	/// <summary>
	/// Gets the caller-facing view: a live projection of <see cref="Items" /> that cannot be mutated
	/// through any cast.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>Returning <see cref="Items" /> as an <see cref="IReadOnlyDictionary{TKey,TValue}" /> would
	/// not be read-only.</strong> The declared type is, but the runtime type is
	/// <see cref="Dictionary{TKey,TValue}" />, which also implements
	/// <see cref="IDictionary{TKey,TValue}" /> — so a single cast from any assembly, needing no internals
	/// access, could clear the set or forge an entry. A read-only annotation over a mutable runtime type
	/// is a comment, not a guarantee.
	/// </para>
	/// <para>
	/// <see cref="ReadOnlyDictionary{TKey,TValue}" /> is a live wrapper rather than a copy, so a later
	/// write is visible through it and a sink reading it allocates nothing. Its
	/// <see cref="IDictionary{TKey,TValue}" /> implementation throws, which is what closes the cast.
	/// Cached in a field so the wrapper is allocated at most once per context.
	/// </para>
	/// </remarks>
	internal IReadOnlyDictionary<string, string> View => _view ??= new ReadOnlyDictionary<string, string>(Items);

	/// <summary>
	/// Creates an independent copy, for a context that takes ownership of its own propagated set.
	/// </summary>
	/// <remarks>
	/// Used where a context is duplicated in process. The copy is total and safe because every value is
	/// a <see cref="string" />: this is the one feature in the collection with nothing to share by
	/// reference, which is why it can cross an ownership boundary when the others cannot.
	/// </remarks>
	internal MessagePropagationFeature Clone()
	{
		var clone = new MessagePropagationFeature();
		foreach (var entry in Items)
		{
			clone.Items[entry.Key] = entry.Value;
		}

		return clone;
	}
}
