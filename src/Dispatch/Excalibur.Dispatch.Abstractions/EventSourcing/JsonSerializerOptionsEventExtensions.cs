// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Excalibur.Dispatch;

/// <summary>
/// Extension members on <see cref="JsonSerializerOptions"/> exposing the canonical event-serialization
/// contract as a discoverable static accessor.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible",
	Justification = "The 'extension' block is a C# extension-member declaration, not a consumer-visible nested type.")]
public static class JsonSerializerOptionsEventExtensions
{
	extension(JsonSerializerOptions)
	{
		/// <summary>
		/// Gets the single frozen (read-only) canonical <see cref="JsonSerializerOptions"/> used to
		/// serialize and deserialize event payloads across every event store and the default event
		/// serializer.
		/// </summary>
		/// <remarks>
		/// Sugar over <see cref="EventSerializationDefaults.Canonical"/> so adoption sites can read
		/// <c>JsonSerializerOptions.Events</c>. The instance is <see cref="JsonSerializerOptions.IsReadOnly"/>
		/// and must not be mutated; use <see cref="EventSerializationDefaults.CreateCanonicalOptions"/> for a
		/// mutable copy.
		/// </remarks>
		/// <value>The frozen canonical serializer options for event payloads and metadata.</value>
		public static JsonSerializerOptions Events => EventSerializationDefaults.Canonical;
	}
}
