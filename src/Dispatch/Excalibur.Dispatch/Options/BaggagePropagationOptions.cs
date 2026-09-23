// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Options;

/// <summary>
/// Controls which W3C baggage entries are copied from the ambient activity into a message context, and how
/// much of them.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Nothing is copied unless you name it.</strong> Baggage arrives from outside the process — the
/// runtime parses the inbound <c>baggage</c> header into the ambient activity without being asked — so its
/// keys and values are chosen by whoever made the request, not by the application. Copying them into a
/// message context puts them on the wire for remote transports and into the logs, telemetry and stores of
/// every service downstream. The entries worth carrying are few and known, so they are opted in by name.
/// </para>
/// <para>
/// <strong>The caps are not the allowlist, and neither one covers the other.</strong> An allowlist keeps
/// untrusted values out of trusted sinks; it does nothing about size, because a permitted key can carry a
/// large value. The caps bound amplification — one cheap request riding every downstream message — and do
/// nothing about trust. Both apply.
/// </para>
/// </remarks>
public sealed class BaggagePropagationOptions
{
	/// <summary>
	/// Gets the baggage keys that may be copied into a message context.
	/// </summary>
	/// <value>
	/// The permitted keys, compared with the ordinal comparer. Empty by default, which copies nothing.
	/// </value>
	/// <remarks>
	/// A key is matched exactly, without the <c>baggage.</c> prefix the context item receives. Comparison is
	/// ordinal and case-sensitive because baggage keys are transmitted verbatim and two keys differing only in
	/// case are two different keys to every other participant in the trace.
	/// </remarks>
	public ISet<string> AllowedKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the maximum number of baggage entries copied into a single message context.
	/// </summary>
	/// <value>The entry cap. Defaults to <c>8</c>. Must be greater than zero.</value>
	/// <remarks>
	/// Entries beyond the cap are dropped rather than truncated, so a context never carries a partial value.
	/// The cap applies after the allowlist, so it bounds what a permitted key can do rather than substituting
	/// for the decision about which keys are permitted at all.
	/// </remarks>
	public int MaxEntries { get; set; } = 8;

	/// <summary>
	/// Gets or sets the maximum length, in characters, of a single copied baggage value.
	/// </summary>
	/// <value>The per-value cap. Defaults to <c>256</c>. Must be greater than zero.</value>
	/// <remarks>
	/// A value longer than this is dropped, not trimmed. A trimmed value is still a value: it would be read
	/// downstream as though it were the whole thing, and a correlation identifier cut in half is worse than an
	/// absent one because nothing downstream can tell that it is incomplete.
	/// </remarks>
	public int MaxValueLength { get; set; } = 256;

	/// <summary>
	/// Gets or sets the maximum combined length, in characters, of all copied keys and values.
	/// </summary>
	/// <value>The total cap. Defaults to <c>1024</c>. Must be greater than zero.</value>
	/// <remarks>
	/// Reached before the entry cap when a few permitted keys carry large values, which is the shape the
	/// entry cap alone does not bound.
	/// </remarks>
	public int MaxTotalLength { get; set; } = 1024;
}
