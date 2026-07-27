// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Interface for schema registry integration.
/// </summary>
public interface ISchemaRegistry
{
	/// <summary>
	/// Registers a schema for a specific event type and version so it can later be resolved by
	/// <see cref="GetSchemaAsync"/>. Without a registration path a registry can never resolve a schema,
	/// which would force the validation gate to either fail closed or (worse) rubber-stamp every event.
	/// </summary>
	/// <param name="eventType">The CloudEvent type the schema applies to.</param>
	/// <param name="version">The schema version.</param>
	/// <param name="schema">The schema document.</param>
	/// <param name="cancellationToken">A cancellation token for the operation.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RegisterSchemaAsync(string eventType, string version, string schema, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the schema for a specific event type and version.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<string?> GetSchemaAsync(string eventType, string version, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all versions for an event type.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<IReadOnlyList<string>> GetVersionsAsync(string eventType, CancellationToken cancellationToken);
}
