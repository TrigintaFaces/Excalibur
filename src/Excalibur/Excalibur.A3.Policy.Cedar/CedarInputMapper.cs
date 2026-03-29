// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Maps Excalibur authorization types to Cedar JSON request format.
/// </summary>
internal static class CedarInputMapper
{
	/// <summary>
	/// Builds the Cedar authorization request JSON for a local Cedar agent.
	/// </summary>
	/// <remarks>
	/// Produces Cedar-native authorization request format:
	/// <code>
	/// {
	///   "principal": { "type": "User", "id": "..." },
	///   "action":    { "type": "Action", "id": "..." },
	///   "resource":  { "type": "...", "id": "..." },
	///   "context":   { "tenantId": "...", ... }
	/// }
	/// </code>
	/// </remarks>
	internal static byte[] MapToLocalJson(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource)
	{
		var buffer = new System.Buffers.ArrayBufferWriter<byte>();
		using var writer = new Utf8JsonWriter(buffer);

		writer.WriteStartObject();

		// Principal
		writer.WriteStartObject("principal");
		writer.WriteString("type", "User");
		writer.WriteString("id", subject.ActorId);
		writer.WriteEndObject();

		// Action
		writer.WriteStartObject("action");
		writer.WriteString("type", "Action");
		writer.WriteString("id", action.Name);
		writer.WriteEndObject();

		// Resource
		writer.WriteStartObject("resource");
		writer.WriteString("type", resource.Type);
		writer.WriteString("id", resource.Id);
		writer.WriteEndObject();

		// Context
		writer.WriteStartObject("context");
		if (subject.TenantId is not null)
		{
			writer.WriteString("tenantId", subject.TenantId);
		}

		WriteAttributes(writer, subject.Attributes);
		WriteAttributes(writer, action.Attributes);
		WriteAttributes(writer, resource.Attributes);
		writer.WriteEndObject();

		writer.WriteEndObject();
		writer.Flush();

		return buffer.WrittenSpan.ToArray();
	}

	/// <summary>
	/// Builds the Cedar authorization request JSON for Amazon Verified Permissions (AVP).
	/// </summary>
	internal static byte[] MapToAvpJson(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource,
		string policyStoreId)
	{
		var buffer = new System.Buffers.ArrayBufferWriter<byte>();
		using var writer = new Utf8JsonWriter(buffer);

		writer.WriteStartObject();

		writer.WriteString("policyStoreId", policyStoreId);

		// Principal
		writer.WriteStartObject("principal");
		writer.WriteString("entityType", "User");
		writer.WriteString("entityId", subject.ActorId);
		writer.WriteEndObject();

		// Action
		writer.WriteStartObject("action");
		writer.WriteString("actionType", "Action");
		writer.WriteString("actionId", action.Name);
		writer.WriteEndObject();

		// Resource
		writer.WriteStartObject("resource");
		writer.WriteString("entityType", resource.Type);
		writer.WriteString("entityId", resource.Id);
		writer.WriteEndObject();

		writer.WriteEndObject();
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

		foreach (var (key, value) in attributes)
		{
			writer.WriteString(key, value);
		}
	}
}
