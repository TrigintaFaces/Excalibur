// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Extension methods that bridge <see cref="ICdcBuilder"/> to
/// <see cref="ICdcBuilderDiscovery"/> for table discovery features.
/// </summary>
/// <remarks>
/// <para>
/// These extensions allow consumers to call discovery methods directly on
/// <see cref="ICdcBuilder"/> via the fluent API, while the actual contract
/// is defined on the separate <see cref="ICdcBuilderDiscovery"/> interface
/// following the Interface Segregation Principle.
/// </para>
/// </remarks>
public static class CdcBuilderDiscoveryExtensions
{
	/// <summary>
	/// Binds tracked tables from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configSectionPath">
	/// The configuration section path containing an array of table tracking entries
	/// (e.g., <c>"Cdc:Tables"</c>).
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="configSectionPath"/> is null or whitespace.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the builder does not support <see cref="ICdcBuilderDiscovery"/>.
	/// </exception>
	public static ICdcBuilder BindTrackedTables(this ICdcBuilder builder, string configSectionPath)
	{
		if (builder is ICdcBuilderDiscovery discovery)
		{
			return discovery.BindTrackedTables(configSectionPath);
		}

		throw new InvalidOperationException(
			$"The CDC builder of type '{builder.GetType().Name}' does not support table discovery. " +
			$"Ensure the builder implements '{nameof(ICdcBuilderDiscovery)}'.");
	}

	/// <summary>
	/// Enables automatic discovery of tracked tables from registered
	/// <see cref="ICdcTableProvider"/> implementations in DI.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the builder does not support <see cref="ICdcBuilderDiscovery"/>.
	/// </exception>
	public static ICdcBuilder TrackTablesFromHandlers(this ICdcBuilder builder)
	{
		if (builder is ICdcBuilderDiscovery discovery)
		{
			return discovery.TrackTablesFromHandlers();
		}

		throw new InvalidOperationException(
			$"The CDC builder of type '{builder.GetType().Name}' does not support table discovery. " +
			$"Ensure the builder implements '{nameof(ICdcBuilderDiscovery)}'.");
	}
}
