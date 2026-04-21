// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Configures CDC state store persistence settings shared by all providers (table name, configuration binding).
/// </summary>
/// <remarks>
/// <para>
/// This builder is used with <c>WithStateStore</c> on provider-specific CDC builders
/// to configure the state store independently from the CDC source connection.
/// </para>
/// <para>
/// For relational providers (SQL Server, Postgres) that also need schema and connection
/// string configuration, see <see cref="ICdcRelationalStateStoreBuilder"/>.
/// </para>
/// </remarks>
public interface ICdcStateStoreBuilder
{
	/// <summary>
	/// Sets the table name for CDC checkpoint persistence.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder TableName(string tableName);

	/// <summary>
	/// Binds state store options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">
	/// The configuration section path (e.g., "Cdc:State").
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Uses <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with
	/// <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// When the bound section contains a <c>ConnectionString</c> property,
	/// it acts as an implicit <c>WithStateStore</c> call.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ICdcStateStoreBuilder BindConfiguration(string sectionPath);
}
