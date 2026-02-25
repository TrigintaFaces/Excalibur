// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// In-memory implementation of schema registry for testing.
/// </summary>
public sealed class InMemorySchemaRegistry : ISchemaRegistry
{
	private readonly Dictionary<(string eventType, string version), string> _schemas = [];
	private readonly Dictionary<string, List<string>> _versions = [];

	/// <summary>
	/// Retrieves the schema for a specific event type and version from memory.
	/// </summary>
	/// <param name="eventType"> The event type to get the schema for. </param>
	/// <param name="version"> The schema version to retrieve. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> The schema string if found; otherwise, null. </returns>
	public Task<string?> GetSchemaAsync(string eventType, string version, CancellationToken cancellationToken) =>
		Task.FromResult(_schemas.GetValueOrDefault((eventType, version)));

	/// <summary>
	/// Checks if two schema versions are compatible based on the specified compatibility mode.
	/// </summary>
	/// <param name="eventType"> The event type to check compatibility for. </param>
	/// <param name="fromVersion"> The source schema version. </param>
	/// <param name="toVersion"> The target schema version. </param>
	/// <param name="mode"> The compatibility mode to apply. </param>
	/// <returns> True if the versions are compatible; otherwise, false. </returns>
	public bool IsCompatible(string eventType, string fromVersion, string toVersion, SchemaCompatibilityMode mode)
	{
		// Simplified compatibility check
		if (!Version.TryParse(fromVersion, out var from) || !Version.TryParse(toVersion, out var to))
		{
			return false;
		}

		return mode switch
		{
			SchemaCompatibilityMode.Forward => from.Major == to.Major && from.Minor <= to.Minor,
			SchemaCompatibilityMode.Backward => from.Major == to.Major && from.Minor >= to.Minor,
			SchemaCompatibilityMode.Full => from.Major == to.Major,
			_ => false,
		};
	}

	/// <summary>
	/// Gets all available versions for a specific event type from memory.
	/// </summary>
	/// <param name="eventType"> The event type to get versions for. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A read-only list of available versions for the event type. </returns>
	public Task<IReadOnlyList<string>> GetVersionsAsync(string eventType, CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<string>>(
			_versions.TryGetValue(eventType, out var versions)
				? versions.AsReadOnly()
				: Array.Empty<string>());
}
