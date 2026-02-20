// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Models;

using Excalibur.Data.SqlServer.Cdc;

namespace CdcAntiCorruption.SchemaAdapters;

/// <summary>
/// Defines the contract for adapting legacy customer schemas to the current domain model.
/// </summary>
/// <remarks>
/// <para>
/// This interface is part of the anti-corruption layer pattern. It handles schema evolution
/// by mapping legacy column names and data types to the current domain model.
/// </para>
/// <para>
/// Implementations should handle:
/// <list type="bullet">
/// <item><description>Renamed columns (e.g., CustomerName → Name)</description></item>
/// <item><description>Missing columns with sensible defaults</description></item>
/// <item><description>Type conversions and coercion</description></item>
/// <item><description>Multiple schema versions</description></item>
/// </list>
/// </para>
/// </remarks>
public interface ILegacyCustomerSchemaAdapter
{
	/// <summary>
	/// Adapts a CDC data change event to the current domain model format.
	/// </summary>
	/// <param name="changeEvent">The raw CDC data change event.</param>
	/// <returns>
	/// The adapted customer data in the current domain model format,
	/// or <see langword="null"/> if the event cannot be adapted.
	/// </returns>
	AdaptedCustomerData? Adapt(DataChangeEvent changeEvent);

	/// <summary>
	/// Gets a value indicating whether the adapter can handle the given schema version.
	/// </summary>
	/// <param name="changeEvent">The CDC event to check.</param>
	/// <returns>
	/// <see langword="true"/> if the adapter can handle this schema version;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	bool CanAdapt(DataChangeEvent changeEvent);
}
