// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Handlers;
using CdcAntiCorruption.SchemaAdapters;

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.DependencyInjection;

namespace CdcAntiCorruption.Configuration;

/// <summary>
/// Configuration extensions for the CDC Anti-Corruption Layer example.
/// </summary>
public static class CdcConfiguration
{
	/// <summary>
	/// Adds the CDC anti-corruption layer services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// <list type="bullet">
	/// <item><description><see cref="ILegacyCustomerSchemaAdapter"/> - Schema evolution handling</description></item>
	/// <item><description><see cref="IDataChangeHandler"/> - CDC event processing</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// The anti-corruption layer sits between CDC and your domain:
	/// <code>
	/// CDC Source → DataChangeEvent → CustomerSyncHandler → Domain Command → Dispatcher
	///                                      ↓
	///                              SchemaAdapter (handles legacy formats)
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCdcAntiCorruptionLayer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register schema adapter (singleton - stateless)
		_ = services.AddSingleton<ILegacyCustomerSchemaAdapter, LegacyCustomerSchemaAdapter>();

		// Register CDC handler (scoped - per-request lifecycle)
		_ = services.AddScoped<IDataChangeHandler, CustomerSyncHandler>();

		return services;
	}
}
