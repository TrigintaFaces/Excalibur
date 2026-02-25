// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Interface for schema registry integration.
/// </summary>
public interface ISchemaRegistry
{
	/// <summary>
	/// Gets the schema for a specific event type and version.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<string?> GetSchemaAsync(string eventType, string version, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if two schema versions are compatible.
	/// </summary>
	bool IsCompatible(string eventType, string fromVersion, string toVersion, SchemaCompatibilityMode mode);

	/// <summary>
	/// Gets all versions for an event type.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<IReadOnlyList<string>> GetVersionsAsync(string eventType, CancellationToken cancellationToken);
}
