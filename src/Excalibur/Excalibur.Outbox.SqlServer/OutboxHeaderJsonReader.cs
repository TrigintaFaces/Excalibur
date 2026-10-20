// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Reads the closed persisted header shape without reflection. Non-null JSON values
/// remain <see cref="JsonElement"/> instances, matching the existing dictionary reader.
/// </summary>
internal static class OutboxHeaderJsonReader
{
	internal static Dictionary<string, object> Deserialize(string? json) =>
		string.IsNullOrEmpty(json)
			? new Dictionary<string, object>(StringComparer.Ordinal)
			: JsonSerializer.Deserialize(json, OutboxInternalJsonContext.Default.DictionaryStringObject)
			  ?? new Dictionary<string, object>(StringComparer.Ordinal);
}
