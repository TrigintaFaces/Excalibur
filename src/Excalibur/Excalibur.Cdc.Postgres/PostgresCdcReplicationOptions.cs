// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Replication configuration options for the Postgres CDC processor.
/// </summary>
public sealed class PostgresCdcReplicationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use binary protocol for logical replication.
	/// </summary>
	/// <value><see langword="true"/> to use binary protocol; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool UseBinaryProtocol { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to create the replication slot if it doesn't exist.
	/// </summary>
	/// <value><see langword="true"/> to auto-create the slot; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool AutoCreateSlot { get; set; } = true;
}
