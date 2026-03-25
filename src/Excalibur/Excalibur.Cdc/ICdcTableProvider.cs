// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Provider-agnostic interface for types that declare which database tables
/// they handle. Implementations registered in DI are discovered by
/// <see cref="ICdcBuilder.TrackTablesFromHandlers"/> at startup.
/// </summary>
/// <remarks>
/// <para>
/// This interface decouples table discovery from any specific CDC provider.
/// Provider-specific handler interfaces (e.g., <c>IDataChangeHandler</c> in
/// <c>Excalibur.Cdc.SqlServer</c>) can extend this interface, enabling
/// <see cref="ICdcBuilder.TrackTablesFromHandlers"/> to discover tables
/// across all providers without taking a dependency on any specific one.
/// </para>
/// </remarks>
public interface ICdcTableProvider
{
	/// <summary>
	/// Gets the names of the database tables this provider handles.
	/// </summary>
	/// <value>
	/// An array of fully qualified table names (e.g., <c>"dbo.Orders"</c>).
	/// </value>
	string[] TableNames { get; }
}
