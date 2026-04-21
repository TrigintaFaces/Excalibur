// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Defines the contract for configuring Postgres CDC replication-specific options including
/// slot naming, publication, processor identity, and protocol settings.
/// </summary>
public interface IPostgresCdcReplicationBuilder
{
	/// <summary>
	/// Sets the Postgres replication slot name.
	/// </summary>
	/// <param name="slotName">The replication slot name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder ReplicationSlotName(string slotName);

	/// <summary>
	/// Sets the Postgres publication name.
	/// </summary>
	/// <param name="publicationName">The publication name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder PublicationName(string publicationName);

	/// <summary>
	/// Sets the processor identifier for this CDC processor instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder ProcessorId(string processorId);

	/// <summary>
	/// Sets whether to use binary protocol for logical replication.
	/// </summary>
	/// <param name="useBinary">Whether to use binary protocol.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder UseBinaryProtocol(bool useBinary = true);

	/// <summary>
	/// Sets whether to automatically create the replication slot if it doesn't exist.
	/// </summary>
	/// <param name="autoCreate">Whether to auto-create the slot.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder AutoCreateSlot(bool autoCreate = true);
}
