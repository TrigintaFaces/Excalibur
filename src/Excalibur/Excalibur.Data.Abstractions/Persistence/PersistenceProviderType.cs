// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines the types of persistence providers supported.
/// </summary>
public enum PersistenceProviderType
{
	/// <summary>
	/// Microsoft SQL Server provider.
	/// </summary>
	SqlServer = 0,

	/// <summary>
	/// Postgres provider.
	/// </summary>
	Postgres = 1,

	/// <summary>
	/// MongoDB provider.
	/// </summary>
	MongoDB = 2,

	/// <summary>
	/// Elasticsearch provider.
	/// </summary>
	Elasticsearch = 3,

	/// <summary>
	/// Redis provider.
	/// </summary>
	Redis = 4,

	/// <summary>
	/// In-memory provider for testing.
	/// </summary>
	InMemory = 5,

	/// <summary>
	/// Custom provider type.
	/// </summary>
	Custom = 6,
}
