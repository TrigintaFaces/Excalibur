// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Configures the JSON serializer used by the patterns hosting surface (message, outbox, and saga payloads).
/// </summary>
/// <remarks>
/// The serializer establishes its own transport defaults — camelCase naming, case-insensitive reads, null
/// omission, and a pooled buffer profile — and then applies <see cref="ConfigureSerializer" /> last, so a
/// delegate here can override any of them. Configuration is expressed as a delegate rather than a
/// pre-built options instance because the serializer must layer consumer settings <em>over</em> its
/// defaults; handing it a finished instance would silently discard them.
/// </remarks>
public sealed class DispatchPatternsJsonOptions
{
	/// <summary>
	/// Gets or sets a delegate applied to the serializer's options after its defaults are established.
	/// </summary>
	/// <value> <see langword="null" /> to accept the defaults unchanged. </value>
	/// <example>
	/// <code>
	/// services.AddJsonSerialization(o =&gt; o.ConfigureSerializer = json =&gt; json.WriteIndented = true);
	/// </code>
	/// </example>
	public Action<JsonSerializerOptions>? ConfigureSerializer { get; set; }

	/// <summary>
	/// Gets or sets the source-generated context used for serialization.
	/// </summary>
	/// <value>
	/// <see langword="null" /> to use the built-in context. Native AOT consumers must supply a context
	/// covering their payload types, because no reflection fallback is available under AOT.
	/// </value>
	public JsonSerializerContext? SerializerContext { get; set; }
}
