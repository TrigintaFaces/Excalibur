// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Schema registry for managing message schemas in Pub/Sub.
/// </summary>
public interface ISchemaRegistry
{
	/// <summary>
	/// Registers a schema for a message type.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<SchemaMetadata> RegisterSchemaAsync(Type messageType, string schema, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the schema for a message type.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<SchemaMetadata?> GetSchemaAsync(Type messageType, CancellationToken cancellationToken);

	/// <summary>
	/// Validates a message against its registered schema.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task<bool> ValidateAsync(object message, CancellationToken cancellationToken);
}
