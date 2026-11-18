// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Controls which inbound HTTP request headers are copied into a message context, and how much of them.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Nothing is copied unless you name it.</strong> Request headers are chosen by whoever made the
/// request, and they carry credentials by construction — <c>Authorization</c> and <c>Cookie</c> are
/// headers like any other. A value copied into a message context is put on the wire by every remote
/// transport, written into metadata, and persisted by at least one outbox provider, so a header crosses
/// that boundary only when the application has asked for it by name.
/// </para>
/// <para>
/// <strong>The caps are not the allowlist, and neither one covers the other.</strong> An allowlist keeps
/// untrusted values out of trusted sinks; it does nothing about size, because a permitted header can
/// carry a large value. The caps bound amplification — one cheap request riding every downstream message
/// — and do nothing about trust. Both apply.
/// </para>
/// <para>
/// A captured header is placed under a <c>header.</c> prefix, matching the <c>baggage.</c> convention used
/// for W3C baggage, so values that arrived from outside the process share one grammar. <strong>That prefix
/// is a legibility aid, not a security boundary:</strong> the context item collection is public, so any
/// caller can write a <c>header.</c> key directly. Nothing downstream may treat the prefix as proof of
/// provenance.
/// </para>
/// </remarks>
public sealed class RequestHeaderCaptureOptions
{
	/// <summary>
	/// Gets the request header names that may be copied into a message context.
	/// </summary>
	/// <value>
	/// The permitted header names, compared case-insensitively. Empty by default, which copies nothing.
	/// </value>
	/// <remarks>
	/// A name is matched exactly, without the <c>header.</c> prefix the context item receives. Comparison
	/// is case-insensitive because HTTP header field names are case-insensitive by specification, so
	/// <c>X-Request-Id</c> and <c>x-request-id</c> are the same header and must not be two allowlist
	/// entries.
	/// </remarks>
	public ISet<string> AllowedHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets the maximum number of headers copied into a single message context.
	/// </summary>
	/// <value>The entry cap. Defaults to <c>8</c>. Must be greater than zero.</value>
	/// <remarks>
	/// Headers beyond the cap are dropped rather than truncated, so a context never carries a partial
	/// value. The cap applies after the allowlist, so it bounds what a permitted header can do rather than
	/// substituting for the decision about which headers are permitted at all. Which headers are dropped
	/// is decided in ordinal name order rather than by arrival order, so the captured set is a function of
	/// the request rather than of enumeration order.
	/// </remarks>
	public int MaxEntries { get; set; } = 8;

	/// <summary>
	/// Gets or sets the maximum length, in characters, of a single copied header value.
	/// </summary>
	/// <value>The per-value cap. Defaults to <c>256</c>. Must be greater than zero.</value>
	/// <remarks>
	/// A value longer than this is dropped, not trimmed. A trimmed value is still a value: it would be
	/// read downstream as though it were the whole thing, and a request identifier cut in half is worse
	/// than an absent one because nothing downstream can tell that it is incomplete.
	/// </remarks>
	public int MaxValueLength { get; set; } = 256;

	/// <summary>
	/// Gets or sets the maximum combined length, in characters, of all copied names and values.
	/// </summary>
	/// <value>The total cap. Defaults to <c>1024</c>. Must be greater than zero.</value>
	/// <remarks>
	/// Reached before the entry cap when a few permitted headers carry large values, which is the shape
	/// the entry cap alone does not bound.
	/// </remarks>
	public int MaxTotalLength { get; set; } = 1024;
}
