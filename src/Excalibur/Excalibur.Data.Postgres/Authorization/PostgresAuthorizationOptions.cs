// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// Configures where the Postgres activity-group store keeps its table.
/// </summary>
/// <remarks>
/// <para>
/// The activity-group table carries no application identifier, so a catalogue replace applies to every row
/// in it. <b>Two applications that share a database must therefore each use their own schema</b>; with the
/// default they would replace each other's catalogue on every sync.
/// </para>
/// <para>
/// The shipped script creates the table in <c>authz</c>. To use another schema, run the script with
/// <c>authz</c> replaced by the value configured here.
/// </para>
/// <para>
/// This option does not yet move the grant tables, which the grant store still addresses in <c>authz</c>.
/// </para>
/// </remarks>
public sealed class PostgresAuthorizationOptions
{
	/// <summary>
	/// Gets or sets the schema that holds the <c>activity_group</c> table.
	/// </summary>
	/// <value>
	/// An identifier of ASCII letters, digits and underscores. The default is <c>authz</c>.
	/// </value>
	public string SchemaName { get; set; } = "authz";
}
