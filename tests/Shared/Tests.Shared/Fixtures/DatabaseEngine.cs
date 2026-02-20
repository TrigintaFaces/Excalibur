// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

namespace Tests.Shared.Fixtures;

/// <summary>
/// Identifies the type of database engine used in container fixtures.
/// </summary>
public enum DatabaseEngine
{
	/// <summary>
	/// Microsoft SQL Server database engine.
	/// </summary>
	SqlServer,

	/// <summary>
	/// Postgres database engine.
	/// </summary>
	Postgres,

	/// <summary>
	/// Elasticsearch search and analytics engine.
	/// </summary>
	Elasticsearch,

	/// <summary>
	/// MongoDB document database engine.
	/// </summary>
	MongoDb,
}
