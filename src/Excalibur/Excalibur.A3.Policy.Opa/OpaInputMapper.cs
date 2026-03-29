// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Policy.Opa;

/// <summary>
/// Maps Excalibur authorization types to OPA JSON input format.
/// </summary>
internal static class OpaInputMapper
{
	/// <summary>
	/// Builds the OPA input JSON payload from authorization request types.
	/// </summary>
	/// <remarks>
	/// Produces JSON in the OPA standard input format:
	/// <code>
	/// {
	///   "input": {
	///     "subject": { "actorId": "...", "tenantId": "...", "attributes": { ... } },
	///     "action":  { "name": "...", "attributes": { ... } },
	///     "resource": { "type": "...", "id": "...", "attributes": { ... } }
	///   }
	/// }
	/// </code>
	/// </remarks>
	internal static byte[] MapToInputJson(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource)
	{
		var buffer = new System.Buffers.ArrayBufferWriter<byte>();
		using var writer = new Utf8JsonWriter(buffer);

		writer.WriteStartObject(); // root
		writer.WriteStartObject("input");

		// Subject
		writer.WriteStartObject("subject");
		writer.WriteString("actorId", subject.ActorId);
		if (subject.TenantId is not null)
		{
			writer.WriteString("tenantId", subject.TenantId);
		}

		WriteAttributes(writer, subject.Attributes);
		writer.WriteEndObject();

		// Action
		writer.WriteStartObject("action");
		writer.WriteString("name", action.Name);
		WriteAttributes(writer, action.Attributes);
		writer.WriteEndObject();

		// Resource
		writer.WriteStartObject("resource");
		writer.WriteString("type", resource.Type);
		writer.WriteString("id", resource.Id);
		WriteAttributes(writer, resource.Attributes);
		writer.WriteEndObject();

		writer.WriteEndObject(); // input
		writer.WriteEndObject(); // root
		writer.Flush();

		return buffer.WrittenSpan.ToArray();
	}

	private static void WriteAttributes(
		Utf8JsonWriter writer,
		IReadOnlyDictionary<string, string>? attributes)
	{
		if (attributes is null or { Count: 0 })
		{
			return;
		}

		writer.WriteStartObject("attributes");
		foreach (var (key, value) in attributes)
		{
			writer.WriteString(key, value);
		}

		writer.WriteEndObject();
	}
}
