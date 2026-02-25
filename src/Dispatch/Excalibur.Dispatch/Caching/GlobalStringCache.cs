// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Global string encoding cache for common values.
/// </summary>
public static class GlobalStringCache
{
	private static readonly Lazy<StringEncodingCache> _instance = new(static () => new StringEncodingCache(2000));

	static GlobalStringCache() =>

		// Preload common JSON field names
		Instance.Preload(
			"id", "MessageId", "ID",
			"type", "Type", "TYPE",
			"timestamp", "Timestamp", "TIMESTAMP",
			"version", "Version", "VERSION",
			"data", "Data", "DATA",
			"message", "Message", "MESSAGE",
			"error", "Error", "ERROR",
			"status", "Status", "STATUS",
			"name", "Name", "NAME",
			"value", "Value", "VALUE",
			"true", "false", "null");

	/// <summary>
	/// Gets the shared global instance of the string encoding cache.
	/// </summary>
	/// <value> The singleton encoding cache for shared string lookups. </value>
	public static StringEncodingCache Instance => _instance.Value;
}
