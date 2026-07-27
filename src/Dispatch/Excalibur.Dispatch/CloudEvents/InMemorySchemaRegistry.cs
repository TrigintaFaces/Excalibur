// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// In-memory implementation of schema registry for testing.
/// </summary>
internal sealed class InMemorySchemaRegistry : ISchemaRegistry
{
	private readonly ConcurrentDictionary<(string eventType, string version), string> _schemas = new();
	private readonly ConcurrentDictionary<string, List<string>> _versions = new();
	private readonly System.Threading.Lock _versionsLock = new();

	/// <summary>
	/// Registers a schema in memory so it can be resolved by <see cref="GetSchemaAsync"/> and enumerated by
	/// <see cref="GetVersionsAsync"/>. Without this the store was permanently empty (every lookup returned
	/// null), which made the validation gate inert.
	/// </summary>
	/// <param name="eventType"> The event type the schema applies to. </param>
	/// <param name="version"> The schema version. </param>
	/// <param name="schema"> The schema document. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A completed task. </returns>
	public Task RegisterSchemaAsync(string eventType, string version, string schema, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		ArgumentException.ThrowIfNullOrWhiteSpace(version);
		ArgumentNullException.ThrowIfNull(schema);
		cancellationToken.ThrowIfCancellationRequested();

		_schemas[(eventType, version)] = schema;

		lock (_versionsLock)
		{
			var versions = _versions.GetOrAdd(eventType, static _ => []);
			if (!versions.Contains(version, StringComparer.Ordinal))
			{
				versions.Add(version);
			}
		}

		return Task.CompletedTask;
	}

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
	/// Gets all available versions for a specific event type from memory.
	/// </summary>
	/// <param name="eventType"> The event type to get versions for. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A read-only list of available versions for the event type. </returns>
	public Task<IReadOnlyList<string>> GetVersionsAsync(string eventType, CancellationToken cancellationToken)
	{
		// Snapshot under the same lock RegisterSchemaAsync mutates the list with, so a concurrent
		// registration never races an enumeration.
		lock (_versionsLock)
		{
			return Task.FromResult<IReadOnlyList<string>>(
				_versions.TryGetValue(eventType, out var versions)
					? versions.ToArray()
					: Array.Empty<string>());
		}
	}
}
