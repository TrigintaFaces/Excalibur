// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Represents supported database providers.
/// </summary>
public enum DatabaseProvider
{
	/// <summary>
	/// Microsoft SQL Server.
	/// </summary>
	SqlServer = 0,

	/// <summary>
	/// Postgres database.
	/// </summary>
	Postgres = 1,

	/// <summary>
	/// MySQL database.
	/// </summary>
	MySql = 2,

	/// <summary>
	/// SQLite database.
	/// </summary>
	Sqlite = 3,

	/// <summary>
	/// MongoDB NoSQL database.
	/// </summary>
	MongoDb = 4,

	/// <summary>
	/// Redis cache/database.
	/// </summary>
	Redis = 5,
}
