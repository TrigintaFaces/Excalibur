// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance.Postgres;
using Excalibur.Compliance.Postgres.Erasure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the full Postgres compliance surface (erasure, data-inventory,
/// and legal-hold stores) behind a single builder that shares one connection across all three stores.
/// </summary>
/// <remarks>
/// Mirrors the AWS entry point <c>AddAwsKmsKeyManagement(Action&lt;IComplianceAwsBuilder&gt;)</c>: a
/// single fluent registration configures a shared Postgres connection and wires the erasure,
/// data-inventory, and legal-hold stores together. For a single store use the focused
/// <c>AddPostgresErasureStore</c> / <c>AddPostgresDataInventoryStore</c> / <c>AddPostgresLegalHoldStore</c>
/// extensions instead.
/// </remarks>
public static class PostgresComplianceServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Postgres erasure, data-inventory, and legal-hold compliance stores, sharing one
	/// connection configured via the builder across all three.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configures the shared Postgres connection for the compliance stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// services.AddPostgresCompliance(pg =&gt;
	///     pg.ConnectionString("Host=localhost;Database=compliance;Username=app;Password=secret"));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use the ConnectionString/DataSource overloads.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use the ConnectionString/DataSource overloads.")]
	public static IServiceCollection AddPostgresCompliance(
		this IServiceCollection services,
		Action<IPostgresComplianceBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var erasureOptions = new PostgresErasureStoreOptions();
		var inventoryOptions = new PostgresDataInventoryStoreOptions();
		var legalHoldOptions = new PostgresLegalHoldStoreOptions();

		var builder = new PostgresComplianceBuilder(erasureOptions, inventoryOptions, legalHoldOptions);
		configure(builder);

		// Register all three stores. The direct ConnectionString mode is already applied to the option
		// instances by the builder; deferred connection sources are resolved onto the DI-managed options below.
		_ = services.AddPostgresErasureStore(o => o.ConnectionString = erasureOptions.ConnectionString);
		_ = services.AddPostgresDataInventoryStore(o => o.ConnectionString = inventoryOptions.ConnectionString);
		_ = services.AddPostgresLegalHoldStore(o => o.ConnectionString = legalHoldOptions.ConnectionString);

		ApplyDeferredConnection(services, builder);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use the ConnectionString/DataSource overloads.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use the ConnectionString/DataSource overloads.")]
	private static void ApplyDeferredConnection(IServiceCollection services, PostgresComplianceBuilder builder)
	{
		if (builder.DataSourceInstance is { } dataSource)
		{
			var connectionString = dataSource.ConnectionString;
			ForEachStore(services, o => o.ConnectionString = connectionString, o => o.ConnectionString = connectionString, o => o.ConnectionString = connectionString);
		}
		else if (builder.DataSourceFactoryFunc is { } factory)
		{
			_ = services.AddOptions<PostgresErasureStoreOptions>().Configure<IServiceProvider>((o, sp) => o.ConnectionString = factory(sp).ConnectionString);
			_ = services.AddOptions<PostgresDataInventoryStoreOptions>().Configure<IServiceProvider>((o, sp) => o.ConnectionString = factory(sp).ConnectionString);
			_ = services.AddOptions<PostgresLegalHoldStoreOptions>().Configure<IServiceProvider>((o, sp) => o.ConnectionString = factory(sp).ConnectionString);
		}
		else if (builder.ConnectionStringNameValue is { } name)
		{
			_ = services.AddOptions<PostgresErasureStoreOptions>().Configure<IConfiguration>((o, cfg) => o.ConnectionString = ResolveNamed(cfg, name));
			_ = services.AddOptions<PostgresDataInventoryStoreOptions>().Configure<IConfiguration>((o, cfg) => o.ConnectionString = ResolveNamed(cfg, name));
			_ = services.AddOptions<PostgresLegalHoldStoreOptions>().Configure<IConfiguration>((o, cfg) => o.ConnectionString = ResolveNamed(cfg, name));
		}
		else if (builder.BindConfigurationPath is { } path)
		{
			_ = services.AddOptions<PostgresErasureStoreOptions>().BindConfiguration(path);
			_ = services.AddOptions<PostgresDataInventoryStoreOptions>().BindConfiguration(path);
			_ = services.AddOptions<PostgresLegalHoldStoreOptions>().BindConfiguration(path);
		}
	}

	private static string ResolveNamed(IConfiguration configuration, string name) =>
		configuration.GetConnectionString(name)
			?? throw new InvalidOperationException($"No connection string named '{name}' was found in configuration.");

	private static void ForEachStore(
		IServiceCollection services,
		Action<PostgresErasureStoreOptions> erasure,
		Action<PostgresDataInventoryStoreOptions> inventory,
		Action<PostgresLegalHoldStoreOptions> legalHold)
	{
		_ = services.Configure(erasure);
		_ = services.Configure(inventory);
		_ = services.Configure(legalHold);
	}
}
