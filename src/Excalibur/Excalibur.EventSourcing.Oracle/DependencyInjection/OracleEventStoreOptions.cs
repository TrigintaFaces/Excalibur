// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Oracle.DependencyInjection;

/// <summary>
/// Configuration options for an individual Oracle event store registration.
/// </summary>
/// <remarks>
/// Use this with <c>AddOracleEventStore(Action&lt;OracleEventStoreOptions&gt;)</c> for ergonomic
/// per-store configuration without needing a raw <c>Func&lt;OracleConnection&gt;</c>.
/// </remarks>
public sealed class OracleEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Oracle connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the event store table. Default: "EXCALIBUR".
	/// </summary>
	public string Schema { get; set; } = "EXCALIBUR";

	/// <summary>
	/// Gets or sets the event store table name. Default: "EVENTSTOREEVENTS".
	/// </summary>
	public string Table { get; set; } = "EVENTSTOREEVENTS";
}
