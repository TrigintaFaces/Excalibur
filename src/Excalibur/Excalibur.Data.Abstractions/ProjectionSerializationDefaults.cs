// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data;

/// <summary>
/// Canonical <see cref="JsonSerializerOptions"/> for projection read-model persistence.
/// </summary>
/// <remarks>
/// <para>
/// A projection is a queryable read-model, not an event. This contract is deliberately distinct from the
/// event-payload serializer: it uses camelCase property naming and omits null properties for a compact,
/// consumer-facing document shape, but it <b>preserves the numeric representation of enums</b> (no
/// string-enum converter). Read-model JSON is filtered through SQL / search predicates where an
/// enum-as-string would break range and equality queries, so numeric enums are part of the read-model
/// contract rather than an incidental default.
/// </para>
/// <para>
/// Every projection store sources its options from this single factory so the persisted document shape
/// stays consistent across providers, while still allowing a consumer to supply their own
/// <see cref="JsonSerializerOptions"/> where they need full control.
/// </para>
/// </remarks>
public static class ProjectionSerializationDefaults
{
	/// <summary>
	/// Creates a new mutable <see cref="JsonSerializerOptions"/> instance carrying the projection
	/// read-model contract: camelCase naming, null-property omission, and numeric enum values.
	/// </summary>
	/// <returns>A fresh options instance the caller owns and may further customize.</returns>
	public static JsonSerializerOptions CreateReadModelOptions() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};
}
